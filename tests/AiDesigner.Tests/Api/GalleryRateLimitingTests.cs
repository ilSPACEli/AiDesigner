using System.Net;
using System.Text.Json;
using AiDesigner.Api;
using AiDesigner.Services.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AiDesigner.Tests.Api;

// Обмеження частоти (AC-24): понад SavesPerMinute збережень за хвилину з однієї адреси → 429.
// Перевіряється на мінімальному хості з TestServer: лише політика, без решти застосунку.
public class GalleryRateLimitingTests
{
    // ---------- Збереження в межах ліміту ----------

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task AC24_Rate_RequestOverLimitFromSameAddress_Returns429(int limit)
    {
        await using var app = await LimitedApp.StartAsync(limit);

        for (var i = 0; i < limit; i++)
            Assert.Equal(HttpStatusCode.Created, (await app.PostAsync()).StatusCode);

        var rejected = await app.PostAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task AC24_Rate_RejectedResponse_IsProblemDetailsWithRetryAfter()
    {
        await using var app = await LimitedApp.StartAsync(savesPerMinute: 1);
        await app.PostAsync();

        var rejected = await app.PostAsync();

        Assert.Equal(ApiProblem.ContentType, rejected.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal(429, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(GalleryRateLimiting.TooManyRequestsTitle, json.RootElement.GetProperty("title").GetString());
        Assert.Equal("Ліміт збережень — 1 за хвилину. Спробуйте пізніше.", json.RootElement.GetProperty("detail").GetString());
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    // ---------- «З однієї адреси» ----------

    [Fact]
    public async Task AC24_Rate_DifferentAddresses_AreCountedSeparately()
    {
        await using var app = await LimitedApp.StartAsync(savesPerMinute: 2);
        await app.PostAsync(ip: "10.0.0.1");
        await app.PostAsync(ip: "10.0.0.1");

        var sameAddress = await app.PostAsync(ip: "10.0.0.1");
        var otherAddress = await app.PostAsync(ip: "10.0.0.2");

        Assert.Equal(HttpStatusCode.TooManyRequests, sameAddress.StatusCode);
        Assert.Equal(HttpStatusCode.Created, otherAddress.StatusCode);
    }

    // ---------- Обмежується лише збереження ----------

    [Fact]
    public async Task Rate_OtherEndpoints_AreNotLimited()
    {
        await using var app = await LimitedApp.StartAsync(savesPerMinute: 1);

        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await app.GetAsync("/other")).StatusCode);
    }

    // ---------- Налаштування ----------

    [Fact]
    public async Task Rate_NonPositiveLimit_FailsOnFirstUse()
    {
        // Помилкове Gallery__SavesPerMinute=0 не має мовчки вимкнути чи заблокувати збереження.
        await using var app = await LimitedApp.StartAsync(savesPerMinute: 0, expectStartupFailure: true);

        Assert.True(app.StartupFailed);
    }

    private sealed class LimitedApp : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly HttpClient? _client;

        public bool StartupFailed { get; }

        private LimitedApp(WebApplication app, HttpClient? client, bool startupFailed)
        {
            _app = app;
            _client = client;
            StartupFailed = startupFailed;
        }

        public static async Task<LimitedApp> StartAsync(int savesPerMinute, bool expectStartupFailure = false)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(new GalleryOptions { SavesPerMinute = savesPerMinute });
            builder.Services.AddGalleryRateLimiting();

            var app = builder.Build();
            app.Use((context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Ip", out var ip))
                    context.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                return next(context);
            });
            app.UseRateLimiter();
            app.MapPost("/save", () => Results.StatusCode(StatusCodes.Status201Created))
                .RequireRateLimiting(GalleryRateLimiting.SaveResultPolicy);
            app.MapGet("/other", () => "ok");

            try
            {
                await app.StartAsync();
            }
            catch (Exception ex) when (expectStartupFailure
                && (ex is ArgumentOutOfRangeException || ex.InnerException is ArgumentOutOfRangeException))
            {
                // Помилку налаштування хост може обернути в іншу виняткову ситуацію, тому дивимося й на InnerException.
                return new LimitedApp(app, client: null, startupFailed: true);
            }

            var server = (TestServer)app.Services.GetRequiredService<IServer>();
            return new LimitedApp(app, server.CreateClient(), startupFailed: false);
        }

        public Task<HttpResponseMessage> PostAsync(string ip = "10.0.0.1")
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/save");
            request.Headers.Add("X-Test-Ip", ip);
            return _client!.SendAsync(request);
        }

        public Task<HttpResponseMessage> GetAsync(string path, string ip = "10.0.0.1")
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Test-Ip", ip);
            return _client!.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            _client?.Dispose();
            await _app.DisposeAsync();
        }
    }
}