using AiDesigner.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AiDesigner.Data;

public class AppDbContext : DbContext
{
    public const int LightingsMaxLength = 50;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    public DbSet<SavedResult> SavedResults => Set<SavedResult>();

    private static readonly ValueConverter<IReadOnlyList<LightingLevel>, string> LightingsConverter = new(
        levels => string.Join(',', levels),
        text => text.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => Enum.Parse<LightingLevel>(name))
            .ToArray());

    private static readonly ValueComparer<IReadOnlyList<LightingLevel>> LightingsComparer = new(
        (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
        levels => levels.Aggregate(0, (hash, level) => HashCode.Combine(hash, level)),
        levels => levels.ToArray());

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogItem>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).HasMaxLength(50);

            item.Property(i => i.Name).HasMaxLength(200).IsRequired();

            item.Property(i => i.Type).HasConversion<string>().HasMaxLength(20);

            item.Property(i => i.ImageFile).HasMaxLength(300).IsRequired();
            item.Property(i => i.MainColor).HasMaxLength(7).IsRequired();   // #RRGGBB
            item.Property(i => i.LicenseSource).HasMaxLength(500).IsRequired();

            item.Property(i => i.SuitableLightings)
                .HasConversion(LightingsConverter, LightingsComparer)
                .HasMaxLength(LightingsMaxLength)
                .IsRequired();
        });

        modelBuilder.Entity<SavedResult>(result =>
        {
            result.HasKey(r => r.Id);

            result.Property(r => r.CreatedAt)
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            result.Property(r => r.CatalogItemId).HasMaxLength(50).IsRequired();
            result.Property(r => r.ImageData).IsRequired();
            result.Property(r => r.ThumbnailData).IsRequired();

            result.HasOne(r => r.CatalogItem)
                .WithMany()
                .HasForeignKey(r => r.CatalogItemId)
                .OnDelete(DeleteBehavior.Restrict);

            result.HasIndex(r => new { r.CreatedAt, r.Id });
        });
    }
}
