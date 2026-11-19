using System.Data.Common;
using AiDesigner.Data;
using AiDesigner.Models;
using AiDesigner.Repositories;
using AiDesigner.Services.Results;
using AiDesigner.Tests.Analysis;
using AiDesigner.Tests.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp.PixelFormats;

namespace AiDesigner.Tests.Repositories;

public sealed class ResultAnalysisTransactionTests : IDisposable
{
    private const string ItemId = "sofa-01";

    private readonly TestDb _testDb = new();

    public ResultAnalysisTransactionTests()
    {
        using var db = _testDb.CreateContext();
        db.CatalogItems.Add(new CatalogItem
        {
            Id = ItemId, Name = "Сірий диван", Type = ItemType.SOFA, ImageFile = "images/catalog/sofa-01.png",
            MainColor = "#82817A", SuitableLightings = new[] { LightingLevel.MODERATE }, LicenseSource = "test"
        });
        db.SaveChanges();
    }

    public void Dispose() => _testDb.Dispose();

    private static ResultAnalysis Analysis() => new()
    {
        Mode = AnalysisMode.LOCAL_AI,
        Lighting = LightingLevel.MODERATE,
        Colors = new[] { new DominantColor { Hex = "#D8C3A5", Hue = 36, Share = 100m } },
        Style = RoomStyle.BOHO,
        Confidence = 0.7m
    };

    private static SavedResult Work(DateTime createdAt) => new()
    {
        CreatedAt = createdAt,
        CatalogItemId = ItemId,
        ImageData = new byte[] { 1 },
        ThumbnailData = new byte[] { 2 },
        Analysis = Analysis()
    };

    private (int Works, int Analyses) Count()
    {
        using var db = _testDb.CreateContext();
        return (db.SavedResults.Count(), db.ResultAnalyses.Count());
    }

    [Fact]
    public async Task AC32_Add_SavesWorkAndAnalysis()
    {
        using var db = _testDb.CreateContext();

        var saved = await new EfResultRepository(db).AddAsync(Work(DateTime.UtcNow));

        Assert.Equal((1, 1), Count());
        using var check = _testDb.CreateContext();
        Assert.Equal(saved.Id, check.ResultAnalyses.Single().SavedResultId);
    }

    [Fact]
    public async Task AC33_AnalysisInsertFails_NoWorkNoAnalysis_EvictionRolledBack()
    {
        using (var db = _testDb.CreateContext())
            await new EfResultRepository(db).AddAsync(Work(DateTime.UtcNow.AddMinutes(-1)));

        using (var failing = _testDb.CreateContext(new FailAnalysisInsert()))
        {
            await Assert.ThrowsAnyAsync<Exception>(() =>
                new EfResultRepository(failing).AddAsync(Work(DateTime.UtcNow), evictOldest: 1));
        }

        Assert.Equal((1, 1), Count());
    }

    [Fact]
    public async Task AC33_Service_AnalysisInsertFails_Returns500Outcome_NothingSaved()
    {
        using var failing = _testDb.CreateContext(new FailAnalysisInsert());
        var service = new ResultService(
            new EfResultRepository(failing), new EfCatalogRepository(failing),
            new GalleryOptions(), NullLogger<ResultService>.Instance);

        var outcome = await service.SaveAsync(TestImages.Solid(40, 30, new Rgb24(200, 100, 50)), ItemId, Analysis());

        Assert.Equal(SaveStatus.Failed, outcome.Status);
        Assert.Equal((0, 0), Count());
    }

    [Fact]
    public async Task DeleteWork_DeletesAnalysis()
    {
        int id;
        using (var db = _testDb.CreateContext())
            id = (await new EfResultRepository(db).AddAsync(Work(DateTime.UtcNow))).Id;

        using (var db = _testDb.CreateContext())
            Assert.True(await new EfResultRepository(db).DeleteAsync(id));

        Assert.Equal((0, 0), Count());
    }

    private sealed class FailAnalysisInsert : DbCommandInterceptor
    {
        private static void ThrowIfAnalysisInsert(DbCommand command)
        {
            if (command.CommandText.Contains("INSERT INTO \"ResultAnalyses\"", StringComparison.Ordinal))
                throw new InvalidOperationException("Імітована відмова запису аналізу.");
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            ThrowIfAnalysisInsert(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfAnalysisInsert(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            ThrowIfAnalysisInsert(command);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfAnalysisInsert(command);
            return ValueTask.FromResult(result);
        }
    }
}
