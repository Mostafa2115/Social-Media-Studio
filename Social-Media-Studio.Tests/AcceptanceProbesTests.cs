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
        using var db = CreateInMemoryDbContext();
        var (ingestion, variants, _, _, _) = CreateTestServices(db);
        var validator = new ConstraintValidator();

        var post = await ingestion.IngestPostAsync(new IngestPostRequest
        {
            Title = "Mastering Distributed Systems Reliability",
            Content = "Distributed systems require resilience against network timeouts, idempotency for retries, and strict boundaries. In this guide, we dive deep into reliable message publishing."
        });

        var generatedVariants = await variants.GenerateVariantsAsync(post.Id);

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
        using var db = CreateInMemoryDbContext();
        var (ingestion, variants, _, _, _) = CreateTestServices(db);

        var post = await ingestion.IngestPostAsync(new IngestPostRequest
        {
            Title = "Sample Article",
            Content = "Sample post content for testing platform rule violations."
        });

        var oversizedXContent = new string('A', 290) + " #Tech";
        var exMaxLength = await Assert.ThrowsAsync<ConstraintViolationException>(() =>
            variants.CreateCustomVariantAsync(post.Id, new CreateCustomVariantRequest
            {
                Platform = "X",
                Content = oversizedXContent
            }));

        Assert.Equal("MaxLengthExceeded", exMaxLength.BrokenRule);
        Assert.Contains("exceeds X maximum allowed length of 280", exMaxLength.Message);

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
        using var db = CreateInMemoryDbContext();
        var (ingestion, variants, _, scheduler, _) = CreateTestServices(db);

        var post = await ingestion.IngestPostAsync(new IngestPostRequest
        {
            Title = "Draft Post",
            Content = "Some text for an unapproved draft."
        });

        var createdVariants = await variants.GenerateVariantsAsync(post.Id);
        var draftVariant = createdVariants.First();

        Assert.Equal(VariantStatus.Draft, draftVariant.Status);

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
        using var db = CreateInMemoryDbContext();
        var (ingestion, variants, review, scheduler, _) = CreateTestServices(db);

        var post = await ingestion.IngestPostAsync(new IngestPostRequest
        {
            Title = "Real Target Publish Demo",
            Content = "Testing end-to-end publish flow to target channel with link tracking."
        });

        var createdVariants = await variants.GenerateVariantsAsync(post.Id);
        var telegramVariant = createdVariants.First(v => v.Platform == "Telegram");

        await review.ApproveVariantAsync(telegramVariant.Id);

        var slot = await scheduler.ScheduleVariantAsync(telegramVariant.Id, new ScheduleVariantRequest
        {
            ScheduledTimeUtc = DateTime.UtcNow.AddSeconds(-1)
        });

        int processed = await scheduler.ProcessDueSlotsAsync();
        Assert.Equal(1, processed);

        var history = await scheduler.GetPublishHistoryAsync();
        var attempt = Assert.Single(history);

        Assert.True(attempt.IsSuccess);
        Assert.NotNull(attempt.ResponsePayload);
        Assert.Contains("t.me", attempt.ResponsePayload);

        var updatedSlot = await scheduler.GetSlotByIdAsync(slot.Id);
        Assert.Equal(SlotStatus.Completed, updatedSlot!.Status);
        Assert.Equal(VariantStatus.Published, updatedSlot.PostVariant!.Status);
    }

    [Fact]
    public async Task PROBE_5_ForcePublishRetry_WorkerInterrupted_YieldsExactlyOnePost_ZeroDuplicates()
    {
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

        int pass1 = await scheduler.ProcessDueSlotsAsync();
        Assert.Equal(1, pass1);

        slot.Status = SlotStatus.Processing;
        await db.SaveChangesAsync();

        int pass2 = await scheduler.ProcessDueSlotsAsync();

        var history = await scheduler.GetPublishHistoryAsync();
        var successfulAttempts = history.Where(h => h.ScheduleSlotId == slot.Id && h.IsSuccess).ToList();
        
        Assert.Single(successfulAttempts);
        Assert.Equal(SlotStatus.Completed, slot.Status);
    }

    [Fact]
    public async Task PROBE_6_SwapAdapterInConfiguration_PublishesThroughMockWithoutCodeChanges()
    {
        using var db = CreateInMemoryDbContext();
        var config = new Dictionary<string, string?>
        {
            ["Publishers:Telegram"] = "MockX"
        };
        var (ingestion, variants, review, scheduler, resolver) = CreateTestServices(db, config);

        var resolvedPublisher = resolver.GetPublisher("Telegram");
        Assert.Equal("X", resolvedPublisher.PlatformName);
        Assert.IsType<MockXPublisher>(resolvedPublisher);

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

        await scheduler.ProcessDueSlotsAsync();

        var history = await scheduler.GetPublishHistoryAsync();
        var attempt = Assert.Single(history);
        Assert.True(attempt.IsSuccess);
        Assert.Contains("x.com/mock_user/status", attempt.ResponsePayload);
    }
}
