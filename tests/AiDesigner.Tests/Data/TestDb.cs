using AiDesigner.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AiDesigner.Tests.Data;

// Окрема БД для кожного тесту: SQLite
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        _connection.Open();

        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    // Перехоплювачі (interceptors) — щоб у тесті втрутитися в роботу EF, напр. імітувати інший процес.
    public AppDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptors)
            .Options);

    public void Dispose() => _connection.Dispose();
}
