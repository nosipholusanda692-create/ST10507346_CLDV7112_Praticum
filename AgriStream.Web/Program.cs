using AgriStream.Web.Data;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// (f) Monitoring: reads APPLICATIONINSIGHTS_CONNECTION_STRING from the environment
builder.Services.AddApplicationInsightsTelemetry();

// (b) Azure SQL Database - users + roles
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SqlDb"),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

// Cookie authentication + role based authorization
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/Denied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// (c) Blob + Table storage
builder.Services.AddSingleton<StorageService>();

// Client used to call the HTTP-triggered POST Azure Function
builder.Services.AddHttpClient("functions", c =>
{
    var baseUrl = builder.Configuration["Functions:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl) && !baseUrl.Contains('<'))
        c.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

    var key = builder.Configuration["Functions:Key"];
    if (!string.IsNullOrWhiteSpace(key) && !key.Contains('<'))
        c.DefaultRequestHeaders.Add("x-functions-key", key);

    c.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Create tables + seed roles and the first admin user
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db, app.Configuration);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Used by App Service health check, load tests and autoscale smoke tests
app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
