using AiDesigner.Models;

namespace AiDesigner.Tests;


public class DesignSessionTests
{
    // ---------- Тестові дані ----------

    private static Photo CreatePhoto(string name = "room.jpg") => new()
    {
        FileName = name,
        Format = "jpeg",
        Width = 800,
        Height = 600,
        SizeBytes = 1000,
        Content = new byte[] { 1, 2, 3 }
    };

    private static AnalysisResult CreateAnalysis(int colorCount = 3) => new()
    {
        Colors = Enumerable.Range(0, colorCount)
            .Select(i => new DominantColor { Hex = "#808080", Hue = i * 60, Share = 100m / colorCount })
            .ToList(),
        Lighting = LightingLevel.MODERATE,
        AnalyzedAt = DateTime.Now
    };

    private static CatalogItem CreateItem(string id) => new()
    {
        Id = id,
        Name = "Предмет " + id,
        Type = ItemType.LAMP,
        ImageFile = $"data/items/{id}.png",
        MainColor = "#FFFFFF",
        SuitableLightings = new[] { LightingLevel.MODERATE },
        LicenseSource = "test"
    };

    private static List<Suggestion> CreateSuggestions(params string[] ids) =>
        ids.Select((id, i) => new Suggestion { Item = CreateItem(id), Rank = i + 1, Score = 1, Reason = "тест" })
           .ToList();

    private static DesignSession CreateSessionIn(SessionState state)
    {
        var session = new DesignSession();
        if (state == SessionState.EMPTY) return session;

        session.ReplacePhoto(CreatePhoto());
        if (state == SessionState.PHOTO_LOADED) return session;

        session.BeginAnalysis();
        session.CompleteAnalysis(CreateAnalysis());
        if (state == SessionState.ANALYZED) return session;

        var suggestions = CreateSuggestions("a", "b", "c");
        session.ShowSuggestions(suggestions);
        if (state == SessionState.SUGGESTIONS_SHOWN) return session;

        session.PlaceItem(new Placement(suggestions[0].Item, 100, 100));
        if (state == SessionState.ITEM_PLACED) return session;

        session.MarkSaved(new ResultImage { FileName = "result.png", CreatedAt = DateTime.Now });
        return session;
    }

    // ---------- Основний шлях EMPTY → … → SAVED ----------

    [Fact]
    public void NewSession_IsEmpty()
    {
        var session = new DesignSession();

        Assert.Equal(SessionState.EMPTY, session.State);
        Assert.Null(session.Photo);
        Assert.False(string.IsNullOrEmpty(session.Id));
    }

    [Theory]
    [InlineData(SessionState.PHOTO_LOADED)]
    [InlineData(SessionState.ANALYZED)]
    [InlineData(SessionState.SUGGESTIONS_SHOWN)]
    [InlineData(SessionState.ITEM_PLACED)]
    [InlineData(SessionState.SAVED)]
    public void MainPath_ReachesEachState(SessionState target)
    {
        var session = CreateSessionIn(target);

        Assert.Equal(target, session.State);
    }

    // ---------- Недозволені переходи ----------

    [Fact]
    public void BeginAnalysis_WithoutPhoto_Throws()
    {
        var session = new DesignSession();

        Assert.Throws<InvalidOperationException>(() => session.BeginAnalysis());
    }

    [Fact]
    public void ShowSuggestions_BeforeAnalysis_Throws()
    {
        var session = CreateSessionIn(SessionState.PHOTO_LOADED);

        Assert.Throws<InvalidOperationException>(() => session.ShowSuggestions(CreateSuggestions("a", "b", "c")));
    }

    [Fact]
    public void PlaceItem_BeforeSuggestions_Throws()
    {
        var session = CreateSessionIn(SessionState.ANALYZED);

        Assert.Throws<InvalidOperationException>(() => session.PlaceItem(new Placement(CreateItem("a"), 0, 0)));
    }

    [Fact]
    public void MarkSaved_BeforePlacement_Throws()
    {
        var session = CreateSessionIn(SessionState.SUGGESTIONS_SHOWN);

        Assert.Throws<InvalidOperationException>(
            () => session.MarkSaved(new ResultImage { FileName = "r.png" }));
    }

    [Fact]
    public void PlaceItem_NotFromSuggestions_Throws()
    {
        var session = CreateSessionIn(SessionState.SUGGESTIONS_SHOWN);

        Assert.Throws<ArgumentException>(() => session.PlaceItem(new Placement(CreateItem("zzz"), 0, 0)));
    }

    // ---------- Аналіз (стани для UC-02) ----------

    [Fact]
    public void FailAnalysis_KeepsPhotoLoaded_WithoutResult()
    {
        var session = CreateSessionIn(SessionState.PHOTO_LOADED);

        session.BeginAnalysis();
        session.FailAnalysis();

        Assert.Equal(SessionState.PHOTO_LOADED, session.State);
        Assert.Null(session.Analysis);
        Assert.False(session.IsAnalyzing);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void CompleteAnalysis_WrongColorCount_Throws(int colorCount)
    {
        var session = CreateSessionIn(SessionState.PHOTO_LOADED);
        session.BeginAnalysis();

        Assert.Throws<ArgumentException>(() => session.CompleteAnalysis(CreateAnalysis(colorCount)));
    }

    // ---------- FR-10: інший предмет ----------

    [Fact]
    public void PlaceOtherItem_ReplacesPlacement_KeepsPhotoAndAnalysis()
    {
        var session = CreateSessionIn(SessionState.ITEM_PLACED);
        var photo = session.Photo;
        var analysis = session.Analysis;
        var other = session.Suggestions[1].Item;

        session.PlaceItem(new Placement(other, 50, 50));

        Assert.Equal(SessionState.ITEM_PLACED, session.State);
        Assert.Same(other, session.Placement!.Item);
        Assert.Same(photo, session.Photo);
        Assert.Same(analysis, session.Analysis);
    }

    // ---------- FR-13: заміна фото (AC-13) ----------

    [Theory]
    [InlineData(SessionState.PHOTO_LOADED)]
    [InlineData(SessionState.ANALYZED)]
    [InlineData(SessionState.SUGGESTIONS_SHOWN)]
    [InlineData(SessionState.ITEM_PLACED)]
    [InlineData(SessionState.SAVED)]
    public void AC13_ReplacePhoto_ResetsResults_AndReturnsToPhotoLoaded(SessionState from)
    {
        var session = CreateSessionIn(from);
        var newPhoto = CreatePhoto("new.jpg");

        session.ReplacePhoto(newPhoto);

        Assert.Equal(SessionState.PHOTO_LOADED, session.State);
        Assert.Same(newPhoto, session.Photo);
        Assert.Null(session.Analysis);
        Assert.Empty(session.Suggestions);
        Assert.Null(session.Placement);
    }

    [Fact]
    public void AC13_ReplacePhoto_DuringAnalysis_IsBlocked()
    {
        var session = CreateSessionIn(SessionState.PHOTO_LOADED);
        var oldPhoto = session.Photo;
        session.BeginAnalysis();

        Assert.Throws<InvalidOperationException>(() => session.ReplacePhoto(CreatePhoto("new.jpg")));
        Assert.Same(oldPhoto, session.Photo);
    }
}
