using GhostTrace.Core.Localization;
using Xunit;

namespace GhostTrace.Tests.Unit.Core.Localization;

public class LocTests
{
    [Theory]
    [InlineData("pt", "pt")]
    [InlineData("pt-BR", "pt")]
    [InlineData("es", "es")]
    [InlineData("es-ES", "es")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("fr", "en")]   // unknown → English fallback
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void SetLanguage_ResolvesTwoLetterCode(string? input, string expected)
    {
        Loc.SetLanguage(input);
        Assert.Equal(expected, Loc.ActiveLanguage);
    }

    [Fact]
    public void Portuguese_OverridesStrings()
    {
        Loc.SetLanguage("pt-BR");
        Assert.Equal("Buscar rastros de software", Loc.Current.MenuHuntTitle);
        Assert.Equal("SIM", Loc.Current.ConfirmWord);
        Assert.Equal("S", Loc.Current.AffirmativeKey);
    }

    [Fact]
    public void Spanish_OverridesStrings()
    {
        Loc.SetLanguage("es");
        Assert.Equal("Buscar rastros de software", Loc.Current.MenuHuntTitle);
        Assert.Equal("SÍ", Loc.Current.ConfirmWord);
    }

    [Fact]
    public void English_IsDefaultAndComplete()
    {
        Loc.SetLanguage("en");
        Assert.Equal("Search software traces", Loc.Current.MenuHuntTitle);
        Assert.Equal("YES", Loc.Current.ConfirmWord);
        // A format string keeps its placeholder.
        Assert.Contains("{0}", Loc.Current.MatchesTitleFmt);
    }

    [Fact]
    public void AllLanguages_HaveNoNullStrings()
    {
        foreach (var locale in new[] { Loc.English, Loc.Portuguese, Loc.Spanish })
        {
            foreach (var prop in typeof(LocaleStrings).GetProperties())
            {
                var value = prop.GetValue(locale) as string;
                Assert.False(string.IsNullOrEmpty(value), $"{prop.Name} is empty");
            }
        }
    }
}
