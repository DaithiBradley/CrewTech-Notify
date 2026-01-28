using CrewTech.Notify.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrewTech.Notify.Infrastructure.Data;

/// <summary>
/// Entity Framework DbContext for the notification system
/// </summary>
public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<NotificationMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(256);
            
            entity.HasIndex(e => e.IdempotencyKey)
                .IsUnique();
            
            entity.Property(e => e.TargetPlatform)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.DeviceToken)
                .IsRequired()
                .HasMaxLength(1024);
            
            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(512);
            
            entity.Property(e => e.Body)
                .IsRequired()
                .HasMaxLength(4096);
            
            entity.Property(e => e.Data)
                .HasMaxLength(8192);
            
            entity.Property(e => e.Tags)
                .HasMaxLength(1024);
            
            entity.Property(e => e.Priority)
                .HasMaxLength(20);
            
            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>();
            
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Status, e.ScheduledFor });
        });
    }
}
