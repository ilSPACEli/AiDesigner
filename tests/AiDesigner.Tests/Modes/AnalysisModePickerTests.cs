using AiDesigner.Models;
using AiDesigner.Services;
using AiDesigner.Clients.Design;
using AiDesigner.Services.Style;

namespace AiDesigner.Tests.Modes;

// Сторінка «Дизайн»: які режими аналізу показати і що робити після «Аналіз» (FR-18, NFR-04).
public class AnalysisModePickerTests
{
    private static ModeOption Find(IReadOnlyList<ModeOption> options, AnalysisMode mode) =>
        options.Single(o => o.Mode == mode);

    [Fact]
    public void AC26_NoAiAvailable_OnlyBasicEnabled_OthersWithServiceReason()
    {
        var modes = new[]
        {
            ModeAvailability.Available(AnalysisMode.BASIC),
            ModeAvailability.Unavailable(AnalysisMode.LOCAL_AI, OllamaStyleAnalyzer.NotRunningReason),
            ModeAvailability.Unavailable(AnalysisMode.CLOUD_AI, GeminiStyleAnalyzer.NoKeyReason)
        };

        var options = AnalysisModePicker.Options(modes);

        Assert.True(Find(options, AnalysisMode.BASIC).IsEnabled);
        Assert.Null(Find(options, AnalysisMode.BASIC).Reason);

        var local = Find(options, AnalysisMode.LOCAL_AI);
        Assert.False(local.IsEnabled);
        Assert.Equal(OllamaStyleAnalyzer.NotRunningReason, local.Reason);

        var cloud = Find(options, AnalysisMode.CLOUD_AI);
        Assert.False(cloud.IsEnabled);
        Assert.Equal(GeminiStyleAnalyzer.NoKeyReason, cloud.Reason);
    }

    [Fact]
    public void Options_AlwaysInFixedOrder_WithDisplayNames()
    {
        var options = AnalysisModePicker.Options(null);

        Assert.Equal(new[] { AnalysisMode.BASIC, AnalysisMode.LOCAL_AI, AnalysisMode.CLOUD_AI },
            options.Select(o => o.Mode));
        Assert.Equal(new[] { "Аналіз без ШІ", "Локальний ШІ", "Хмарний ШІ" }, options.Select(o => o.Label));
    }

    [Fact]
    public void Options_AllAvailable_AllEnabledWithoutReason()
    {
        var modes = Enum.GetValues<AnalysisMode>().Select(ModeAvailability.Available).ToList();

        var options = AnalysisModePicker.Options(modes);

        Assert.All(options, o =>
        {
            Assert.True(o.IsEnabled);
            Assert.Null(o.Reason);
        });
    }

    [Fact]
    public void Options_WhileChecking_AiModesDisabled_BasicEnabled()
    {
        var options = AnalysisModePicker.Options(null);

        Assert.True(Find(options, AnalysisMode.BASIC).IsEnabled);
        Assert.Equal(AnalysisModePicker.CheckingReason, Find(options, AnalysisMode.LOCAL_AI).Reason);
        Assert.Equal(AnalysisModePicker.CheckingReason, Find(options, AnalysisMode.CLOUD_AI).Reason);
        Assert.False(Find(options, AnalysisMode.LOCAL_AI).IsEnabled);
        Assert.False(Find(options, AnalysisMode.CLOUD_AI).IsEnabled);
    }

    [Fact]
    public void Options_BasicAlwaysEnabled_EvenIfServiceSaysOtherwise()
    {
        var modes = new[] { ModeAvailability.Unavailable(AnalysisMode.BASIC, "щось пішло не так") };

        var basic = Find(AnalysisModePicker.Options(modes), AnalysisMode.BASIC);

        Assert.True(basic.IsEnabled);
        Assert.Null(basic.Reason);
    }

    [Fact]
    public void Options_ModeMissingInServiceAnswer_NotConfigured()
    {
        var modes = new[] { ModeAvailability.Available(AnalysisMode.BASIC) };

        var local = Find(AnalysisModePicker.Options(modes), AnalysisMode.LOCAL_AI);

        Assert.False(local.IsEnabled);
        Assert.Equal(DesignService.ModeNotConfiguredReason, local.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Options_UnavailableWithoutReason_GenericReason(string? reason)
    {
        var modes = new[] { new ModeAvailability(AnalysisMode.CLOUD_AI, false, reason) };

        var cloud = Find(AnalysisModePicker.Options(modes), AnalysisMode.CLOUD_AI);

        Assert.Equal(AnalysisModePicker.UnknownReason, cloud.Reason);
    }

    [Fact]
    public void OptionsAfterCheckFailed_AiModesDisabled_BasicEnabled()
    {
        var options = AnalysisModePicker.OptionsAfterCheckFailed();

        Assert.True(Find(options, AnalysisMode.BASIC).IsEnabled);
        Assert.Equal(DesignService.ModeCheckFailedReason, Find(options, AnalysisMode.LOCAL_AI).Reason);
        Assert.Equal(DesignService.ModeCheckFailedReason, Find(options, AnalysisMode.CLOUD_AI).Reason);
    }

    [Fact]
    public void DefaultMode_IsBasic()
    {
        Assert.Equal(AnalysisMode.BASIC, AnalysisModePicker.DefaultMode);
    }

    [Fact]
    public void KeepSelectable_AvailableMode_Kept()
    {
        var options = AnalysisModePicker.Options(Enum.GetValues<AnalysisMode>().Select(ModeAvailability.Available).ToList());

        Assert.Equal(AnalysisMode.LOCAL_AI, AnalysisModePicker.KeepSelectable(AnalysisMode.LOCAL_AI, options));
    }

    [Fact]
    public void KeepSelectable_ModeBecameUnavailable_FallsBackToBasic()
    {
        var options = AnalysisModePicker.Options(new[]
        {
            ModeAvailability.Unavailable(AnalysisMode.LOCAL_AI, OllamaStyleAnalyzer.NotRunningReason)
        });

        Assert.Equal(AnalysisMode.BASIC, AnalysisModePicker.KeepSelectable(AnalysisMode.LOCAL_AI, options));
    }

    [Fact]
    public void Warning_LocalAi_QualityDependsOnModel()
    {
        Assert.Equal(OllamaStyleAnalyzer.QualityWarning, AnalysisModePicker.Warning(AnalysisMode.LOCAL_AI));
        Assert.Contains("залежить від локальної моделі", AnalysisModePicker.Warning(AnalysisMode.LOCAL_AI));
    }

    [Theory]
    [InlineData(AnalysisMode.BASIC)]
    [InlineData(AnalysisMode.CLOUD_AI)]
    public void Warning_OtherModes_None(AnalysisMode mode)
    {
        Assert.Null(AnalysisModePicker.Warning(mode));
    }

    [Fact]
    public void AC31_CloudWithoutConsent_AsksConsent()
    {
        Assert.Equal(AnalyzeStep.AskCloudConsent, AnalysisModePicker.NextStep(AnalysisMode.CLOUD_AI, hasCloudConsent: false));
    }

    [Fact]
    public void AC31_CloudWithConsent_Runs()
    {
        Assert.Equal(AnalyzeStep.Run, AnalysisModePicker.NextStep(AnalysisMode.CLOUD_AI, hasCloudConsent: true));
    }

    [Theory]
    [InlineData(AnalysisMode.BASIC)]
    [InlineData(AnalysisMode.LOCAL_AI)]
    public void AC31_OtherModes_NoConsentNeeded(AnalysisMode mode)
    {
        Assert.Equal(AnalyzeStep.Run, AnalysisModePicker.NextStep(mode, hasCloudConsent: false));
    }

    [Fact]
    public void AC31_ConsentText_NamesServiceAndPhotoTransfer()
    {
        Assert.Contains("Google Gemini", AnalysisModePicker.CloudConsentText);
        Assert.Contains("фото", AnalysisModePicker.CloudConsentText);
        Assert.Equal("Погоджуюсь", AnalysisModePicker.ConsentAcceptLabel);
        Assert.Equal("Скасувати", AnalysisModePicker.ConsentCancelLabel);
    }
}
