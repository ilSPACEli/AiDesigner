using AiDesigner.Data;
using AiDesigner.Repositories;
using AiDesigner.Services.Results;
using AiDesigner.Tests.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiDesigner.Tests.Api;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly TestDb _testDb = new();

    public ApiFactory(GalleryOptions? gallery = null)
    {
        // За замовчуванням ліміт частоти не заважає тестам, яким він не потрібен.
        Gallery = gallery ?? new GalleryOptions { SavesPerMinute = 1000 };
    }

    public GalleryOptions Gallery { get; }

    public InMemoryResultRepository Results { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Production: стартова ініціалізація не викликає Migrate() (міграції написані під Npgsql, а тут SQLite).
        builder.UseEnvironment("Production");

        builder.UseSetting("ConnectionStrings:Default", "Host=unused;Database=unused");

        builder.ConfigureTestServices(services =>
        {
            // Замість Npgsql: SQLite у пам'яті, у який стартова ініціалізація засіває каталог.
            services.RemoveAll<AppDbContext>();
            services.AddScoped(_ => _testDb.CreateContext());

            services.RemoveAll<GalleryOptions>();
            services.AddSingleton(Gallery);

            services.RemoveAll<IResultRepository>();
            services.AddSingleton<IResultRepository>(Results);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _testDb.Dispose();
    }
}
