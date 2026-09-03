using Microsoft.EntityFrameworkCore;
using Social_Media_Studio.Data.Entities;

namespace Social_Media_Studio.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<PostVariant> PostVariants => Set<PostVariant>();
    public DbSet<ScheduleSlot> ScheduleSlots => Set<ScheduleSlot>();
    public DbSet<PublishAttempt> PublishAttempts => Set<PublishAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BlogPost configuration
        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Content).IsRequired();
            entity.HasMany(e => e.Variants)
                  .WithOne(v => v.BlogPost)
                  .HasForeignKey(v => v.BlogPostId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // PostVariant configuration
        modelBuilder.Entity<PostVariant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Platform).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasMany(e => e.ScheduleSlots)
                  .WithOne(s => s.PostVariant)
                  .HasForeignKey(s => s.PostVariantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ScheduleSlot configuration
        modelBuilder.Entity<ScheduleSlot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();

            entity.HasMany(e => e.PublishAttempts)
                  .WithOne(p => p.ScheduleSlot)
                  .HasForeignKey(p => p.ScheduleSlotId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // PublishAttempt configuration
        modelBuilder.Entity<PublishAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Platform).IsRequired().HasMaxLength(50);
        });
    }
}
