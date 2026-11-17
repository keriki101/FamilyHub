using FamilyHub.Web.Components;
using FamilyHub.Web.Data;
using FamilyHub.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Auth/Login");
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.Cookie.Name = "FamilyHub.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, FamilyHubAuthenticationStateProvider>();
builder.Services.AddSingleton(new SharedPasswordValidator(
    builder.Configuration["FAMILYHUB_PASSWORD_HASH"]
        ?? throw new InvalidOperationException(
            "Set FAMILYHUB_PASSWORD_HASH to a PBKDF2-SHA256 hash. See the README for setup instructions.")));
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };
});
builder.Services.AddSingleton<ICalendarClock, SystemCalendarClock>();
builder.Services.AddScoped<CalendarService>();
builder.Services.AddScoped<CalendarDataService>();

var connectionString = builder.Configuration.GetConnectionString("FamilyHub")
    ?? throw new InvalidOperationException("The FamilyHub connection string is missing.");
var sqliteConnectionString = new SqliteConnectionStringBuilder(connectionString);
if (!Path.IsPathRooted(sqliteConnectionString.DataSource))
{
    sqliteConnectionString.DataSource = Path.Combine(
        builder.Environment.ContentRootPath,
        sqliteConnectionString.DataSource);
}

builder.Services.AddDbContextFactory<FamilyHubDbContext>(options =>
    options.UseSqlite(sqliteConnectionString.ToString()));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FamilyHubDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();

    if (!await db.People.AnyAsync())
    {
        var people = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = "Mamma", Color = "#E26D5A" },
            new Person { Id = Guid.NewGuid(), Name = "Pappa", Color = "#D9A441" },
            new Person { Id = Guid.NewGuid(), Name = "Love", Color = "#5B8C7A" }
        };

        db.People.AddRange(people);
        db.Events.AddRange(
            new Event { Id = Guid.NewGuid(), PersonId = people[0].Id, Title = "Jobb", Start = DateTime.Today.AddHours(8), End = DateTime.Today.AddHours(16), IsWorkEvent = true },
            new Event { Id = Guid.NewGuid(), PersonId = people[1].Id, Title = "Kontor", Start = DateTime.Today.AddHours(9), End = DateTime.Today.AddHours(17), IsWorkEvent = true },
            new Event { Id = Guid.NewGuid(), PersonId = people[2].Id, Title = "Möte", Start = DateTime.Today.AddDays(1).AddHours(10), End = DateTime.Today.AddDays(1).AddHours(11) });
        await db.SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages();
app.MapPost("/auth/login", async (
    HttpContext context,
    IAntiforgery antiforgery,
    SharedPasswordValidator passwordValidator) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest("Ogiltig inloggningsförfrågan. Ladda om sidan och försök igen.");
    }

    var form = await context.Request.ReadFormAsync();
    if (!passwordValidator.IsValid(form["Password"]))
    {
        return Results.Redirect("/Auth/Login?failed=1");
    }

    var expiresAt = DateTimeOffset.UtcNow.AddHours(12);
    var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "Family"),
            new Claim(
                FamilyHubAuthenticationStateProvider.SessionExpiresAtClaim,
                expiresAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture))
        ],
        CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);
    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties { IsPersistent = false, ExpiresUtc = expiresAt });

    return Results.LocalRedirect("/");
})
    .AllowAnonymous()
    .RequireRateLimiting("login");
app.MapPost("/auth/logout", async (HttpContext context, IAntiforgery antiforgery) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest("Ogiltig begäran. Ladda om sidan och försök igen.");
    }

    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/Auth/Login");
}).RequireAuthorization();
app.MapRazorComponents<App>()
    .RequireAuthorization()
    .AddInteractiveServerRenderMode();

app.Run();
