using AiDesigner.Models;
using AiDesigner.Services;
using AiDesigner.Services.Stubs;

namespace AiDesigner.Tests;

/// <summary>UC-04: розміщення, переміщення й заміна предмета на рівні DesignService.</summary>
public class DesignServicePlacementTests
{
    // Фото 800×600; предмет 512×492 (як у каталозі).
    private const int PhotoW = 800, PhotoH = 600, ItemW = 512, ItemH = 492;

    private static Photo CreatePhoto() => new()
    {
        FileName = "room.jpg", Format = "jpeg", Width = PhotoW, Height = PhotoH, SizeBytes = 1000,
        Content = new byte[] { 1, 2, 3 }
    };

    // Сервіс у стані SUGGESTIONS_SHOWN (на заглушках).
    private static async Task<DesignService> CreateSuggestedAsync()
    {
        var service = new DesignService(new StubPhotoAnalyzer(), new StubCatalog(), new StubResultStorage());
        service.LoadPhoto(CreatePhoto());
        await service.AnalyzeAsync();
        service.Suggest();
        return service;
    }

    // ---------- AC-08 ----------

    [Fact]
    public async Task AC08_PlaceItemAtCenter_PutsItemInPhotoCenter()
    {
        var service = await CreateSuggestedAsync();
        var item = service.Session.Suggestions[0].Item;

        var shown = service.PlaceItemAtCenter(item, ItemW, ItemH);

        Assert.Equal(SessionState.ITEM_PLACED, service.Session.State);
        Assert.Equal(PhotoW / 2, service.Session.Placement!.X);
        Assert.Equal(PhotoH / 2, service.Session.Placement.Y);
        Assert.Equal(shown.Scale, service.Session.Placement.Scale);
        Assert.Same(item, service.Session.Placement.Item);
    }

    [Fact]
    public void AC08_PlaceItemAtCenter_BeforeSuggestions_Throws()
    {
        var service = new DesignService(new StubPhotoAnalyzer(), new StubCatalog(), new StubResultStorage());
        service.LoadPhoto(CreatePhoto());   // PHOTO_LOADED
        var item = new StubCatalog().Items[0];

        Assert.Throws<InvalidOperationException>(() => service.PlaceItemAtCenter(item, ItemW, ItemH));
        Assert.Null(service.Session.Placement);
    }

    // ---------- AC-09 ----------

    [Fact]
    public async Task AC09_UpdatePlacement_MovesAndResizes()
    {
        var service = await CreateSuggestedAsync();
        service.PlaceItemAtCenter(service.Session.Suggestions[0].Item, ItemW, ItemH);

        var result = service.UpdatePlacement(300, 200, 0.5m, ItemW, ItemH);

        Assert.Equal(new PlacementValues(300, 200, 0.5m), result);
        Assert.Equal(300, service.Session.Placement!.X);
        Assert.Equal(200, service.Session.Placement.Y);
        Assert.Equal(0.5m, service.Session.Placement.Scale);
        Assert.Equal(SessionState.ITEM_PLACED, service.Session.State);
    }

    [Fact]
    public async Task AC09_UpdatePlacement_FarOutside_IsClampedAndStored()
    {
        var service = await CreateSuggestedAsync();
        service.PlaceItemAtCenter(service.Session.Suggestions[0].Item, ItemW, ItemH);

        var result = service.UpdatePlacement(-50000, 50000, 0.5m, ItemW, ItemH);

        Assert.NotEqual(-50000, result.X);
        Assert.NotEqual(50000, result.Y);
        // У сесії те саме, що повернули полотну.
        Assert.Equal(result.X, service.Session.Placement!.X);
        Assert.Equal(result.Y, service.Session.Placement.Y);
    }

    [Fact]
    public async Task AC09_UpdatePlacement_InvalidScale_KeepsPlacement()
    {
        var service = await CreateSuggestedAsync();
        service.PlaceItemAtCenter(service.Session.Suggestions[0].Item, ItemW, ItemH);
        var before = service.Session.Placement!;
        var (x, y, scale) = (before.X, before.Y, before.Scale);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.UpdatePlacement(10, 10, 0m, ItemW, ItemH));

        Assert.Equal((x, y, scale), (service.Session.Placement!.X, service.Session.Placement.Y, service.Session.Placement.Scale));
    }

    [Fact]
    public async Task AC09_UpdatePlacement_BeforePlacement_Throws()
    {
        var service = await CreateSuggestedAsync();   // SUGGESTIONS_SHOWN, предмета ще немає

        Assert.Throws<InvalidOperationException>(() => service.UpdatePlacement(10, 10, 1m, ItemW, ItemH));
    }

    [Fact]
    public async Task AC09_UpdatePlacement_AfterSave_Throws()
    {
        var service = await CreateSuggestedAsync();
        service.PlaceItemAtCenter(service.Session.Suggestions[0].Item, ItemW, ItemH);
        await service.SaveAsync(new byte[] { 1 });   // SAVED

        Assert.Equal(SessionState.SAVED, service.Session.State);
        Assert.Throws<InvalidOperationException>(() => service.UpdatePlacement(10, 10, 1m, ItemW, ItemH));
    }

    // ---------- AC-10 ----------

    [Fact]
    public async Task AC10_ChooseOtherItem_ReplacesItem_KeepsPhotoAndAnalysis()
    {
        var service = await CreateSuggestedAsync();
        service.PlaceItemAtCenter(service.Session.Suggestions[0].Item, ItemW, ItemH);
        service.UpdatePlacement(100, 100, 0.3m, ItemW, ItemH);
        var photo = service.Session.Photo;
        var analysis = service.Session.Analysis;
        var other = service.Session.Suggestions[1].Item;

        service.PlaceItemAtCenter(other, ItemW, ItemH);

        Assert.Same(other, service.Session.Placement!.Item);
        Assert.Equal(PhotoW / 2, service.Session.Placement.X);   // новий предмет знову по центру
        Assert.Same(photo, service.Session.Photo);
        Assert.Same(analysis, service.Session.Analysis);
        Assert.Equal(SessionState.ITEM_PLACED, service.Session.State);
    }

    [Fact]
    public async Task AC10_ChooseOtherItem_AfterSave_ReturnsToItemPlaced()
    {
        var service = await CreateSuggestedAsync();
        service.PlaceItemAtCenter(service.Session.Suggestions[0].Item, ItemW, ItemH);
        await service.SaveAsync(new byte[] { 1 });

        service.PlaceItemAtCenter(service.Session.Suggestions[1].Item, ItemW, ItemH);

        Assert.Equal(SessionState.ITEM_PLACED, service.Session.State);
        // Після повернення в ITEM_PLACED рух знову дозволено.
        service.UpdatePlacement(200, 200, 0.4m, ItemW, ItemH);
    }
}
