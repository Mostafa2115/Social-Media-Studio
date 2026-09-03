using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Social_Media_Studio.Data;
using Social_Media_Studio.Data.Entities;
using Social_Media_Studio.Exceptions;
using Social_Media_Studio.Models.DTOs;
using Social_Media_Studio.Services.Implementations;
using Social_Media_Studio.Services.Interfaces;
using Social_Media_Studio.Services.Publishers;
using Xunit;

namespace Social_Media_Studio.Tests;

public class AcceptanceProbesTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private (IPostIngestionService ingestion, IVariantService variants, IReviewWorkflowService review, ISchedulingService scheduler, IPublisherResolver resolver)
        CreateTestServices(AppDbContext dbContext, Dictionary<string, string?>? configValues = null)
    {
        var inMemorySettings = configValues ?? new Dictionary<string, string?>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var httpClient = new HttpClient();
        var validator = new ConstraintValidator();

        var ingestion = new PostIngestionService(dbContext, httpClient, NullLogger<PostIngestionService>.Instance);
        var variants = new VariantService(dbContext, validator, NullLogger<VariantService>.Instance);
        var review = new ReviewWorkflowService(dbContext, validator, NullLogger<ReviewWorkflowService>.Instance);

        var telegramPublisher = new TelegramPublisher(httpClient, configuration, NullLogger<TelegramPublisher>.Instance);
        var mockXPublisher = new MockXPublisher(NullLogger<MockXPublisher>.Instance);
        var mockLinkedInPublisher = new MockLinkedInPublisher(NullLogger<MockLinkedInPublisher>.Instance);

        var publishers = new List<ISocialPublisher> { telegramPublisher, mockXPublisher, mockLinkedInPublisher };
        var resolver = new PublisherResolver(publishers, configuration, NullLogger<PublisherResolver>.Instance);

        var scheduler = new SchedulingService(dbContext, resolver, NullLogger<SchedulingService>.Instance);

        return (ingestion, variants, review, scheduler, resolver);
    }

    [Fact]
    public async Task PROBE_1_IngestSamplePost_GeneratesValidVariants_PassingConstraintProfiles()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var (ingestion, variants, _, _, _) = CreateTestServices(db);
        var validator = new ConstraintValidator();

        // Act
        var post = await ingestion.IngestPostAsync(new IngestPostRequest
        {
            Title = "Mastering Distributed Systems Reliability",
            Content = "Distributed systems require resilience against network timeouts, idempotency for retries, and strict boundaries. In this guide, we dive deep into reliable message publishing."
        });

        var generatedVariants = await variants.GenerateVariantsAsync(post.Id);

        // Assert
        Assert.Equal(3, generatedVariants.Count);
        foreach (var variant in generatedVariants)
        {
            var result = validator.Validate(variant.Platform, variant.Content);
            Assert.True(result.IsValid, $"Platform {variant.Platform} failed validation: {result.ErrorMessage}");
            Assert.Equal(VariantStatus.Draft, variant.Status);
        }
    }

    [Fact]
    public async Task PROBE_2_CreateVariant_BreakingPlatformRule_BlocksWithNamedErrorBeforeReview()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var (ingestion, variants, _, _, _) = CreateTestServices(db);

        var post = await ingestion.IngestPostAsync(new IngestPostRequest
        {
            Title = "Sample Article",
            Content = "Sample post content for testing platform rule violations."
        });

        // Act & Assert - Case A: Exceeding Max Length for X (280 chars)
        var oversizedXContent = new string('A', 290) + " #Tech";
        var exMaxLength = await Assert.ThrowsAsync<ConstraintViolationException>(() =>
            variants.CreateCustomVariantAsync(post.Id, new CreateCustomVariantRequest
            {
                Platform = "X",
                Content = oversizedXContent
            }));

        Assert.Equal("MaxLengthExceeded", exMaxLength.BrokenRule);
        Assert.Contains("exceeds X maximum allowed length of 280", exMaxLength.Message);

        // Act & Assert - Case B: Missing required hashtags for X (min 1)
        var noHashtagsContent = "Short tweet without any tags.";
        var exMinHashtags = await Assert.ThrowsAsync<ConstraintViolationException>(() =>
            variants.CreateCustomVariantAsync(post.Id, new CreateCustomVariantRequest
            {
                Platform = "X",
                Content = noHashtagsContent
            }));

        Assert.Equal("MinHashtagsNotMet", exMinHashtags.BrokenRule);
    }

    [Fact]
    public async Task PROBE_3_ScheduleUnapprovedVariant_RefusesWithBadRequestAndErrorMessage()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var (ingestion, variants, _, scheduler, _) = CreateTestServices(db);

        var post = await ingestion.IngestPostAsync(new IngestPostRequest
        {
            Title = "Draft Post",
            Content = "Some text for an unapproved draft."
        });

        var createdVariants = await variants.GenerateVariantsAsync(post.Id);
        var draftVariant = createdVariants.First();

        // Ensure status is Draft
        Assert.Equal(VariantStatus.Draft, draftVariant.Status);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scheduler.ScheduleVariantAsync(draftVariant.Id, new ScheduleVariantRequest
            {
                ScheduledTimeUtc = DateTime.UtcNow.AddMinutes(5)
            }));

        Assert.Contains("Only 'Approved' variants can be scheduled", ex.Message);
        Assert.Contains("Draft", ex.Message);
    }

    [Fact]
    public async Task PROBE_4_ApproveAndSchedule_PublishesToRealTarget_AndRecordsLink()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var (ingestion, variants, review, scheduler, _) = CreateTestServices(db);

        var post = await ingestion.IngestPostAsync(new IngestPostRequest
        {
            Title = "Real Target Publish Demo",
            Content = "Testing end-to-end publish flow to target channel with link tracking."
        });

        var createdVariants = await variants.GenerateVariantsAsync(post.Id);
        var telegramVariant = createdVariants.First(v => v.Platform == "Telegram");

        // 1. Approve variant
        await review.ApproveVariantAsync(telegramVariant.Id);

        // 2. Schedule variant (due immediately)
        var slot = await scheduler.ScheduleVariantAsync(telegramVariant.Id, new ScheduleVariantRequest
        {
            ScheduledTimeUtc = DateTime.UtcNow.AddSeconds(-1)
        });

        // 3. Process due slots
        int processed = await scheduler.ProcessDueSlotsAsync();
        Assert.Equal(1, processed);

        // 4. Verify publish history
        var history = await scheduler.GetPublishHistoryAsync();
        var attempt = Assert.Single(history);

        Assert.True(attempt.IsSuccess);
        Assert.NotNull(attempt.ResponsePayload);
        Assert.Contains("t.me", attempt.ResponsePayload);

        // Verify slot and variant statuses
        var updatedSlot = await scheduler.GetSlotByIdAsync(slot.Id);
        Assert.Equal(SlotStatus.Completed, updatedSlot!.Status);
        Assert.Equal(VariantStatus.Published, updatedSlot.PostVariant!.Status);
    }

    [Fact]
    public async Task PROBE_5_ForcePublishRetry_WorkerInterrupted_YieldsExactlyOnePost_ZeroDuplicates()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var (ingestion, variants, review, scheduler, _) = CreateTestServices(db);

        var post = await ingestion.IngestPostAsync(new IngestPostRequest
        {
            Title = "Idempotency Test Post",
            Content = "Testing worker crash recovery and idempotency guarantees under retries."
        });

        var createdVariants = await variants.GenerateVariantsAsync(post.Id);
        var variant = createdVariants.First();

        await review.ApproveVariantAsync(variant.Id);
        var slot = await scheduler.ScheduleVariantAsync(variant.Id, new ScheduleVariantRequest
        {
            ScheduledTimeUtc = DateTime.UtcNow.AddSeconds(-5)
        });

        // First execution pass: succeeds
        int pass1 = await scheduler.ProcessDueSlotsAsync();
        Assert.Equal(1, pass1);

        // Simulate worker crash / mid-batch restart: slot is retried
        slot.Status = SlotStatus.Processing;
        await db.SaveChangesAsync();

        // Second execution pass: worker resumes
        int pass2 = await scheduler.ProcessDueSlotsAsync();

        // Assert: Idempotency check kicked in; exactly 1 successful publish attempt in history!
        var history = await scheduler.GetPublishHistoryAsync();
        var successfulAttempts = history.Where(h => h.ScheduleSlotId == slot.Id && h.IsSuccess).ToList();
        
        Assert.Single(successfulAttempts);
        Assert.Equal(SlotStatus.Completed, slot.Status);
    }

    [Fact]
    public async Task PROBE_6_SwapAdapterInConfiguration_PublishesThroughMockWithoutCodeChanges()
    {
        // Arrange: Swap Telegram publisher to MockX via configuration
        using var db = CreateInMemoryDbContext();
        var config = new Dictionary<string, string?>
        {
            ["Publishers:Telegram"] = "MockX"
        };
        var (ingestion, variants, review, scheduler, resolver) = CreateTestServices(db, config);

        // Verify resolver swapped adapter based on config
        var resolvedPublisher = resolver.GetPublisher("Telegram");
        Assert.Equal("X", resolvedPublisher.PlatformName);
        Assert.IsType<MockXPublisher>(resolvedPublisher);

        // Ingest and approve Telegram variant
        var post = await ingestion.IngestPostAsync(new IngestPostRequest
        {
            Title = "Config Swap Campaign",
            Content = "Testing adapter swap configured via appsettings without altering code."
        });

        var createdVariants = await variants.GenerateVariantsAsync(post.Id);
        var telegramVariant = createdVariants.First(v => v.Platform == "Telegram");
        await review.ApproveVariantAsync(telegramVariant.Id);

        var slot = await scheduler.ScheduleVariantAsync(telegramVariant.Id, new ScheduleVariantRequest
        {
            ScheduledTimeUtc = DateTime.UtcNow.AddSeconds(-1)
        });

        // Act: Process publish
        await scheduler.ProcessDueSlotsAsync();

        // Assert: Published through MockX adapter (x.com preview url recorded)
        var history = await scheduler.GetPublishHistoryAsync();
        var attempt = Assert.Single(history);
        Assert.True(attempt.IsSuccess);
        Assert.Contains("x.com/mock_user/status", attempt.ResponsePayload);
    }
}
