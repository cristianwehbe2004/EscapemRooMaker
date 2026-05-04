using EscapeRoom.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomVersion> RoomVersions => Set<RoomVersion>();
    public DbSet<GameSession> Sessions => Set<GameSession>();
    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();
    public DbSet<SessionSnapshot> SessionSnapshots => Set<SessionSnapshot>();
    public DbSet<RoomRating> RoomRatings => Set<RoomRating>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(256).IsRequired();
            entity.Property(x => x.IsSystem).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.Property(x => x.Role)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasMany(x => x.RefreshTokens)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ExpiresAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("rooms");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.GraphDefinition).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.CreatedByUserId);
            entity.HasIndex(x => x.IsPublished);
        });

        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.ToTable("sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(x => x.StateSnapshot).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.StartedAtUtc).IsRequired();
            entity.Property(x => x.DurationMinutes).IsRequired();
            entity.Property(x => x.LastActivityAtUtc).IsRequired();
            entity.Property(x => x.HostActorId).HasMaxLength(128);
            entity.Property(x => x.IsQuickPlay).IsRequired();
            entity.HasIndex(x => x.RoomId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.EndsAtUtc);
        });

        modelBuilder.Entity<RoomRating>(entity =>
        {
            entity.ToTable("room_ratings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Score).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.RoomId, x.UserId }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("CK_room_ratings_score", "\"Score\" >= 1 AND \"Score\" <= 5"));
        });

        modelBuilder.Entity<RoomVersion>(entity =>
        {
            entity.ToTable("room_versions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GraphDefinition).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.RoomId, x.VersionNumber }).IsUnique();
        });

        modelBuilder.Entity<SessionEvent>(entity =>
        {
            entity.ToTable("session_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EventData).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.OccurredAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SessionId, x.SequenceNumber }).IsUnique();
        });

        modelBuilder.Entity<SessionSnapshot>(entity =>
        {
            entity.ToTable("session_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StateData).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.SessionId, x.Version }).IsUnique();
        });
    }
}
