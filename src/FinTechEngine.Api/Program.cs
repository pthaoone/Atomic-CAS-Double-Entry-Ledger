using FinTechEngine.Api.Infrastructure.Data;
using FinTechEngine.Api.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// SQLite Connection with WAL mode and shared cache for concurrent operations
var dbPath = Path.Combine(AppContext.BaseDirectory, "fintech_ledger.db");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? $"Data Source={dbPath};Cache=Shared;Mode=ReadWriteCreate;";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

// Register Core FinTech Engine Services
builder.Services.AddSingleton<IMetricsCollector, MetricsCollector>();
builder.Services.AddScoped<ITransactionEngine, CasTransactionEngine>();
builder.Services.AddScoped<IUnsafeTransactionEngine, UnsafeTransactionEngine>();
builder.Services.AddScoped<IAuditService, AuditService>();

// Configure CORS for local testing and external scripts
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// 2. Initialize Database & Pragmas
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Optimize SQLite for high concurrency (WAL mode + 15s busy timeout)
    try
    {
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");
        await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=15000;");
    }
    catch
    {
        // Ignore if not supported by current provider
    }

    await AppDbContext.SeedInitialDataAsync(db);
}

// 3. Configure HTTP Pipeline
app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();

// OpenAPI & Scalar API documentation
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "FinTech Core Transaction Engine API";
    options.Theme = ScalarTheme.Purple;
});

app.MapControllers();

app.Run();
