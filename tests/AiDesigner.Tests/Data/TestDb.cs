using AiDesigner.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
