using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TTLockManager.Data;
using TTLockManager.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ───────────────────────────────────────────────────────────────
builder.Services.AddRazorPages(options =>
{
    // Yêu cầu xác thực cho tất cả trang, ngoại trừ trang Login
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
});

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (connectionString != null && (
        connectionString.Contains("Server=") || 
        connectionString.Contains("Database=") || 
        connectionString.Contains("Initial Catalog=") || 
        connectionString.Contains("User Id=") || 
        connectionString.Contains("Password=")))
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString ?? "Data Source=ttlock_manager.db");
    }
});

// TTLock API Client (Singleton để dùng chung token)
builder.Services.AddHttpClient<ITTLockApiClient, TTLockApiClient>();
builder.Services.AddSingleton<ITTLockApiClient, TTLockApiClient>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<TTLockApiClient>>();
    return new TTLockApiClient(http, config, logger);
});

// Đăng ký Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserSessionService>();

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

app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// ── Auto migrate database khi khởi động ───────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
