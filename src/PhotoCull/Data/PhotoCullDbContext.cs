using System.IO;
using Microsoft.EntityFrameworkCore;
using PhotoCull.Models;
using System.Text.Json;

namespace PhotoCull.Data;

public class PhotoCullDbContext : DbContext
{
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<PhotoGroup> PhotoGroups => Set<PhotoGroup>();
    public DbSet<CullingSession> CullingSessions => Set<CullingSession>();

    private readonly string _dbPath;

    public PhotoCullDbContext()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "PhotoCull");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "photocull.db");
    }

    public PhotoCullDbContext(DbContextOptions<PhotoCullDbContext> options) : base(options)
    {
        _dbPath = string.Empty;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={_dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Photo
        modelBuilder.Entity<Photo>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status)
                .HasConversion<string>();

            entity.Property(e => e.Exif)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<PhotoExif>(v, (JsonSerializerOptions?)null) ?? new PhotoExif());

            entity.Property(e => e.AiScore)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<AiScore>(v, (JsonSerializerOptions?)null));

            entity.HasOne(e => e.Group)
                .WithMany(g => g.Photos)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Session)
                .WithMany(s => s.Photos)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PhotoGroup
        modelBuilder.Entity<PhotoGroup>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.GroupType)
                .HasConversion<string>();

            entity.Property(e => e.SelectedPhotoIdsJson)
                .HasColumnName("SelectedPhotoIds");

            entity.Ignore(e => e.SelectedPhotoIds);

            entity.HasOne(e => e.Session)
                .WithMany(s => s.Groups)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CullingSession
        modelBuilder.Entity<CullingSession>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CurrentRound)
                .HasConversion<string>();
        });
    }
}
