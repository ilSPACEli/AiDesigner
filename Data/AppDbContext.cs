using System.Text.Json;
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

    public DbSet<ResultAnalysis> ResultAnalyses => Set<ResultAnalysis>();

    private static readonly ValueConverter<IReadOnlyList<LightingLevel>, string> LightingsConverter = new(
        levels => string.Join(',', levels),
        text => text.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => Enum.Parse<LightingLevel>(name))
            .ToArray());

    private static readonly ValueComparer<IReadOnlyList<LightingLevel>> LightingsComparer = new(
        (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
        levels => levels.Aggregate(0, (hash, level) => HashCode.Combine(hash, level)),
        levels => levels.ToArray());

    private static readonly JsonSerializerOptions ColorsJson = new(JsonSerializerDefaults.Web);

    private static string ColorsToJson(IReadOnlyList<DominantColor> colors) =>
        JsonSerializer.Serialize(colors, ColorsJson);

    private static IReadOnlyList<DominantColor> ColorsFromJson(string json) =>
        JsonSerializer.Deserialize<List<DominantColor>>(json, ColorsJson) ?? new List<DominantColor>();

    private static readonly ValueConverter<IReadOnlyList<DominantColor>, string> ColorsConverter = new(
        colors => ColorsToJson(colors),
        json => ColorsFromJson(json));

    private static readonly ValueComparer<IReadOnlyList<DominantColor>> ColorsComparer = new(
        (a, b) => (a == null && b == null) || (a != null && b != null && ColorsToJson(a) == ColorsToJson(b)),
        colors => ColorsToJson(colors).GetHashCode(),
        colors => ColorsFromJson(ColorsToJson(colors)));

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

            item.Property(i => i.Style).HasConversion<string>().HasMaxLength(20);
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

            result.HasOne(r => r.Analysis)
                .WithOne()
                .HasForeignKey<ResultAnalysis>(a => a.SavedResultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResultAnalysis>(analysis =>
        {
            analysis.HasKey(a => a.SavedResultId);
            analysis.Property(a => a.SavedResultId).ValueGeneratedNever();

            analysis.Property(a => a.Mode).HasConversion<string>().HasMaxLength(20);
            analysis.Property(a => a.Lighting).HasConversion<string>().HasMaxLength(20);
            analysis.Property(a => a.Style).HasConversion<string>().HasMaxLength(20);
            analysis.Property(a => a.Confidence).HasPrecision(4, 3);   // 0.000–1.000

            analysis.Property(a => a.Colors)
                .HasConversion(ColorsConverter, ColorsComparer)
                .IsRequired();
        });
    }
}
