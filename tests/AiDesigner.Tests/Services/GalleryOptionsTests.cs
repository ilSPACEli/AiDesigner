using AiDesigner.Services.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiDesigner.Tests.Services;

// Налаштування Gallery:* перевіряються під час СТАРТУ: помилковий .env зупиняє застосунок при запуску,
// а не падає на першому збереженні чи запиті. Хост ізольований: без appsettings.json і змінних середовища.
public class GalleryOptionsTests
{
    private static IHost BuildHost(params (string Key, string Value)[] settings)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value));
        builder.Services.AddGalleryOptions(builder.Configuration);
        return builder.Build();
    }

    // ---------- Значення ----------

    [Fact]
    public async Task Startup_NoSettings_UsesDefaults()
    {
        using var host = BuildHost();

        await host.StartAsync();

        var options = host.Services.GetRequiredService<GalleryOptions>();
        Assert.Equal(100, options.MaxItems);
        Assert.Equal(5 * 1024 * 1024, options.MaxImageBytes);
        Assert.Equal(5, options.SavesPerMinute);
    }

    [Fact]
    public async Task Startup_BindsSettingsFromGallerySection()
    {
        using var host = BuildHost(
            ("Gallery:MaxItems", "3"),
            ("Gallery:MaxImageBytes", "1000"),
            ("Gallery:SavesPerMinute", "2"));

        await host.StartAsync();

        var options = host.Services.GetRequiredService<GalleryOptions>();
        Assert.Equal(3, options.MaxItems);
        Assert.Equal(1000, options.MaxImageBytes);
        Assert.Equal(2, options.SavesPerMinute);
    }

    // ---------- Некоректні значення зупиняють старт ----------

    [Theory]
    [InlineData("Gallery:MaxItems", "0")]
    [InlineData("Gallery:MaxItems", "-5")]
    [InlineData("Gallery:MaxImageBytes", "0")]
    [InlineData("Gallery:MaxImageBytes", "-1")]
    [InlineData("Gallery:SavesPerMinute", "0")]
    [InlineData("Gallery:SavesPerMinute", "-3")]
    public async Task Startup_NonPositiveSetting_StopsApplication_AndNamesTheKey(string key, string value)
    {
        using var host = BuildHost((key, value));

        var error = await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());

        Assert.Contains(key, error.ToString());
    }

    [Fact]
    public async Task Startup_SeveralInvalidSettings_ReportsAllOfThem()
    {
        using var host = BuildHost(("Gallery:MaxItems", "0"), ("Gallery:SavesPerMinute", "0"));

        var error = await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());

        Assert.Contains("Gallery:MaxItems", error.ToString());
        Assert.Contains("Gallery:SavesPerMinute", error.ToString());
    }
}
