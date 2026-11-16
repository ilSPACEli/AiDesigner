using AiDesigner.Models;

namespace AiDesigner.Tests;

// Назви стилів і режимів для інтерфейсу: у кожного значення переліку — своя непорожня назва.
public class DisplayNamesTests
{
    [Fact]
    public void EveryStyle_HasDistinctName()
    {
        var names = Enum.GetValues<RoomStyle>().Select(s => DisplayNames.Of(s)).ToList();

        Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void EveryMode_HasDistinctName()
    {
        var names = Enum.GetValues<AnalysisMode>().Select(m => DisplayNames.Of(m)).ToList();

        Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void NullStyle_IsNotDetermined()
    {
        Assert.Equal(DisplayNames.StyleNotDetermined, DisplayNames.Of((RoomStyle?)null));
        Assert.Equal("Лофт", DisplayNames.Of((RoomStyle?)RoomStyle.LOFT));
    }

    [Fact]
    public void Styles_MatchSrsList()
    {
        // SRS розд. 4.1: шість стилів.
        Assert.Equal(6, Enum.GetValues<RoomStyle>().Length);
    }
}
