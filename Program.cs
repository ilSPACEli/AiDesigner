using AiDesigner.Components;
using AiDesigner.Data;
using AiDesigner.Services;
using AiDesigner.Services.Analysis;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;


// Налаштування й секрети з файла .env
Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Сервіси v1.0. Заглушки замінюються справжніми реалізаціями в UC-02…UC-04.
builder.Services.AddSingleton<IPhotoAnalyzer, PhotoAnalyzer>();   // UC-02
builder.Services.AddSingleton<ICatalog, Catalog>();
builder.Services.AddSingleton<IResultStorage, FileResultStorage>();

builder.Services.AddScoped<DesignService>();

// БД PostgreSQL через EF Core. Рядок підключення — ConnectionStrings:Default
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Не задано ConnectionStrings:Default. Створіть .env за зразком .env.example.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

// Підготовка БД під час старту: міграції — лише в Development, каталог — див. DatabaseInitializer.
using (var scope = app.Services.CreateScope())
{
    // AppDbContext — Scoped, а запиту ще немає, тому створюємо область вручну.
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var catalogPath = Path.Combine(app.Environment.WebRootPath, CatalogLoader.DefaultRelativePath);

    DatabaseInitializer.Initialize(db, catalogPath, applyMigrations: app.Environment.IsDevelopment(), app.Logger);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// The browser revalidates static files (ETag) on every load, so an outdated
// photoCanvas.js is never used after an update. no-cache keeps the cache: an
// unchanged file costs only a short 304 response.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
});
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
