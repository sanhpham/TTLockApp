using Microsoft.EntityFrameworkCore;
using TTLockManager.Data;
using TTLockManager.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ───────────────────────────────────────────────────────────────
builder.Services.AddRazorPages();

// Database SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// TTLock API Client (Singleton để dùng chung token)
builder.Services.AddHttpClient<ITTLockApiClient, TTLockApiClient>();
builder.Services.AddSingleton<ITTLockApiClient, TTLockApiClient>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<TTLockApiClient>>();
    return new TTLockApiClient(http, config, logger);
});

// Background service tự động polling + freeze + reset
builder.Services.AddHostedService<UsageTrackingBackgroundService>();

var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

// ── Auto migrate database khi khởi động ───────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
