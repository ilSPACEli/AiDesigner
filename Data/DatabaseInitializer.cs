using Microsoft.EntityFrameworkCore;

namespace AiDesigner.Data;

public static class DatabaseInitializer
{
    public static void Initialize(AppDbContext db, string catalogPath, bool applyMigrations, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);

        if (!applyMigrations)
            return;

        db.Database.Migrate();

        var result = CatalogSeeder.Sync(db, catalogPath);
        logger.LogInformation("Каталог у БД: додано {Added}, оновлено {Updated}.", result.Added, result.Updated);
    }
}
