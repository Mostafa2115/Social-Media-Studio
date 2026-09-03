using Microsoft.EntityFrameworkCore;
using Social_Media_Studio.Data;
using Social_Media_Studio.Services.Background;
using Social_Media_Studio.Services.Implementations;
using Social_Media_Studio.Services.Interfaces;
using Social_Media_Studio.Services.Publishers;

var builder = WebApplication.CreateBuilder(args);

// 1. Add DbContext with SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\mssqllocaldb;Database=SocialMediaStudioDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Add HttpClient
builder.Services.AddHttpClient();

// 3. Register Core Services
builder.Services.AddScoped<IPostIngestionService, PostIngestionService>();
builder.Services.AddSingleton<IConstraintValidator, ConstraintValidator>();
builder.Services.AddScoped<IVariantService, VariantService>();
builder.Services.AddScoped<IReviewWorkflowService, ReviewWorkflowService>();
builder.Services.AddScoped<ISchedulingService, SchedulingService>();

// 4. Register Publishers and Resolver
builder.Services.AddScoped<ISocialPublisher, TelegramPublisher>();
builder.Services.AddScoped<ISocialPublisher, MockXPublisher>();
builder.Services.AddScoped<ISocialPublisher, MockLinkedInPublisher>();
builder.Services.AddScoped<IPublisherResolver, PublisherResolver>();

// 5. Register Background Durable Worker
builder.Services.AddHostedService<DurablePublishingWorker>();

// 6. Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Auto-create database tables on startup if not already created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        dbContext.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not automatically create/connect to database on startup. Ensure SQL Server is running.");
    }
}

app.Run();
