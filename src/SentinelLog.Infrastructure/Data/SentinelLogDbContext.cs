using Microsoft.EntityFrameworkCore;
using SentinelLog.Domain.Entities;
using SentinelLog.Domain.Enums;

namespace SentinelLog.Infrastructure.Data;

public class SentinelLogDbContext : DbContext
{
    public SentinelLogDbContext(DbContextOptions<SentinelLogDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- User Configuration ---
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(50);
            entity.HasIndex(u => u.Username).IsUnique();

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();

            entity.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(u => u.IsActive).IsRequired();
            entity.HasIndex(u => u.IsActive);

            entity.Property(u => u.CreatedAt).IsRequired();
        });

        // --- SecurityEvent Configuration ---
        modelBuilder.Entity<SecurityEvent>(entity =>
        {
            entity.ToTable("security_events");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Severity)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Source)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.IPAddress)
                .IsRequired()
                .HasMaxLength(45);

            entity.Property(e => e.UserAgent)
                .HasMaxLength(500);

            entity.Property(e => e.Resource)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.Timestamp)
                .IsRequired();

            entity.Property(e => e.CorrelationId)
                .IsRequired()
                .HasMaxLength(100);

            // Configure PostgreSQL JSONB for Metadata if PostgreSQL is used, fallback to text for InMemory test provider
            if (Database.ProviderName?.Contains("Npgsql") == true)
            {
                entity.Property(e => e.MetadataJson)
                    .HasColumnType("jsonb");
            }

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.HasOne(e => e.Actor)
                .WithMany(u => u.SecurityEvents)
                .HasForeignKey(e => e.ActorId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for fast security querying
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Severity);
            entity.HasIndex(e => e.Source);
            entity.HasIndex(e => e.ActorId);
            entity.HasIndex(e => e.IPAddress);
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => new { e.Timestamp, e.Severity });
            entity.HasIndex(e => new { e.Timestamp, e.EventType });
        });

        // --- AuditLog Configuration ---
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Action)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(a => a.ResourceType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(a => a.ResourceId)
                .HasMaxLength(100);

            entity.Property(a => a.IPAddress)
                .HasMaxLength(45);

            entity.Property(a => a.UserAgent)
                .HasMaxLength(500);

            entity.Property(a => a.Timestamp)
                .IsRequired();

            entity.Property(a => a.Details)
                .HasMaxLength(4000);

            entity.Property(a => a.CorrelationId)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasOne(a => a.Actor)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.ActorId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for audit queries
            entity.HasIndex(a => a.Timestamp);
            entity.HasIndex(a => a.Action);
            entity.HasIndex(a => a.ResourceType);
            entity.HasIndex(a => a.ActorId);
            entity.HasIndex(a => a.CorrelationId);
        });
    }
}
