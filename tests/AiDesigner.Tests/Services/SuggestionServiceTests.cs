using System.Data.Common;
using AiDesigner.Data;
using AiDesigner.Models;
using AiDesigner.Repositories;
using AiDesigner.Services;
using AiDesigner.Services.Suggestions;
using AiDesigner.Tests.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiDesigner.Tests.Services;

// SuggestionService: предмети з репозиторію + правила SuggestionRules; очікувані відмови — значенням, а не винятком.
public class SuggestionServiceTests
{
    private static readonly string CatalogPath = Path.Combine(RepoPaths.WebRoot, CatalogLoader.DefaultRelativePath);

    private static SuggestionService CreateService(ICatalogRepository repository) =>
        new(repository, NullLogger<SuggestionService>.Instance);

    private static CatalogItem Item(string id, ItemType type, string hex, params LightingLevel[] lightings) => new()
    {
        Id = id,
        Name = id,
        Type = type,
        ImageFile = $"images/catalog/{id}.png",
        MainColor = hex,
        SuitableLightings = lightings,
        LicenseSource = "test"
    };

    private static readonly AnalysisResult BlueRoomModerate = Palettes.Of(LightingLevel.MODERATE, Palettes.Rooms["синя"]);

    // Репозиторій, що імітує недоступну БД.
    private sealed class FailingRepository : ICatalogRepository
    {
        public Task<IReadOnlyList<CatalogItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new FakeDbException();

        public Task<CatalogItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
            throw new FakeDbException();
    }

    private sealed class FakeDbException() : DbException("БД недоступна");

    [Fact]
    public async Task Suggest_ReturnsRankedSuggestions()
    {
        var service = CreateService(new InMemoryCatalogRepository(CatalogLoader.LoadFromFile(CatalogPath)));

        var outcome = await service.SuggestAsync(BlueRoomModerate);

        Assert.Equal(SuggestionStatus.Ok, outcome.Status);
        Assert.InRange(outcome.Suggestions.Count, 3, SuggestionService.SuggestionCount);
        Assert.Equal(Enumerable.Range(1, outcome.Suggestions.Count), outcome.Suggestions.Select(s => s.Rank));
    }

    [Fact]
    public async Task Suggest_NoItemForLighting_ReturnsNoSuitableItems()
    {
        var service = CreateService(new InMemoryCatalogRepository(new[]
        {
            Item("lamp", ItemType.LAMP, "#F5F5F5", LightingLevel.BRIGHT),
            Item("sofa", ItemType.SOFA, "#9E9E9E", LightingLevel.BRIGHT)
        }));

        var outcome = await service.SuggestAsync(Palettes.Of(LightingLevel.DARK, Palettes.Rooms["синя"]));

        Assert.Equal(SuggestionStatus.NoSuitableItems, outcome.Status);
        Assert.Empty(outcome.Suggestions);
    }

    [Fact]
    public async Task Suggest_EmptyCatalog_ReturnsCatalogUnavailable()
    {
        var service = CreateService(new InMemoryCatalogRepository(Array.Empty<CatalogItem>()));

        var outcome = await service.SuggestAsync(BlueRoomModerate);

        Assert.Equal(SuggestionStatus.CatalogUnavailable, outcome.Status);
        Assert.Empty(outcome.Suggestions);
    }

    [Fact]
    public async Task Suggest_DatabaseFails_ReturnsCatalogUnavailable()
    {
        var service = CreateService(new FailingRepository());

        var outcome = await service.SuggestAsync(BlueRoomModerate);

        Assert.Equal(SuggestionStatus.CatalogUnavailable, outcome.Status);
    }

    // AC-25 (друга частина) і AC-06 після переходу на БД: каталог із таблиці (синхронізований із catalog.json)
    // дає той самий перелік, що й правила на catalog.json, як у v1.0.0.
    [Theory]
    [MemberData(nameof(Palettes.RoomsAtAllLevels), MemberType = typeof(Palettes))]
    public async Task AC25_CatalogFromDatabase_GivesSameListAsV1(string palette, LightingLevel lighting)
    {
        var analysis = Palettes.Of(lighting, Palettes.Rooms[palette]);
        var expected = new SuggestionRules(CatalogLoader.LoadFromFile(CatalogPath))
            .Select(analysis, SuggestionService.SuggestionCount);

        using var testDb = new TestDb();
        using (var db = testDb.CreateContext())
            CatalogSeeder.Sync(db, CatalogPath);

        using var context = testDb.CreateContext();
        var outcome = await CreateService(new EfCatalogRepository(context)).SuggestAsync(analysis);

        Assert.Equal(SuggestionStatus.Ok, outcome.Status);
        Assert.Equal(expected.Select(s => (s.Item.Id, s.Rank, s.Score, s.Reason)),
                     outcome.Suggestions.Select(s => (s.Item.Id, s.Rank, s.Score, s.Reason)));
        Assert.Equal(DesignService.SuggestionCount, SuggestionService.SuggestionCount);
    }
}
