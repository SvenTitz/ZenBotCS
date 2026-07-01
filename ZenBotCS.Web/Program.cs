using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using ZenBotCS.Entities;
using ZenBotCS.Web;
using ZenBotCS.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Razor / Blazor Server (Interactive Server render mode).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddMudServices();

// Thin query service over the bot DB (keeps components free of EF plumbing).
builder.Services.AddScoped<ZenBotCS.Web.Services.RosterService>();
// Clan name lookup from the CoC cache DB (cached in memory).
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ZenBotCS.Web.Services.ClanNameService>();
// Server-side roster PNG rendering (singleton: loads the bundled font once).
builder.Services.AddSingleton<ZenBotCS.Web.Services.RosterImageService>();
// ClashKing lookups for "Add player" (player name/TH + Discord link). Typed HttpClient = pooled handler.
builder.Services.AddHttpClient<ZenBotCS.Web.Services.ClashKingClient>(c =>
{
    c.BaseAddress = new Uri("https://api.clashk.ing/");
    c.DefaultRequestHeaders.UserAgent.ParseAdd("ZenBot");
    c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

// Direct DB access: the website is a second consumer of the shared BotDataContext.
// Use a DbContextFactory, not a scoped DbContext: in Blazor Server a scoped context lives for the
// whole circuit and is shared across concurrent renders (not thread-safe). Components create a
// short-lived context per operation instead.
// Migrations are owned by the ZenBotCS (bot) project; the website only reads/writes rows.
// Server version is configured explicitly (not AutoDetect) so building the context options never
// opens a connection — the DB is only touched on an actual query. Set "MySqlServerVersion" to match
// your server, e.g. "8.0.0-mysql" or "10.11.0-mariadb".
var botDbConnectionString = builder.Configuration["BotDbConnectionString"]!;
var serverVersion = ServerVersion.Parse(builder.Configuration["MySqlServerVersion"] ?? "8.0.0-mysql");
builder.Services.AddDbContextFactory<BotDataContext>(options =>
    options.UseMySql(botDbConnectionString, serverVersion));

// Authorization config: a single Discord role grants access (like the bot's
// RequireLeadershipRoleAttribute, which checks config["FamilyLeadershipRoleId"]).
// The bot reads roles from the gateway; the site has no gateway, so it looks the member
// up via the Discord REST API with the bot token on login.
var discordGuildId = builder.Configuration["Discord:GuildId"];
var discordBotToken = builder.Configuration["Discord:BotToken"];
var requiredRoleId = builder.Configuration["Discord:RequiredRoleId"];

// Authentication: cookie session, challenged via Discord OAuth.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = DiscordAuthenticationDefaults.AuthenticationScheme;
})
    .AddCookie(options =>
    {
        // Stay signed in for 30 days, sliding (renews when the user visits past the halfway point).
        // Paired with IsPersistent on the login challenge below so the cookie survives a browser close.
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    })
    .AddDiscord(options =>
    {
        options.ClientId = builder.Configuration["Discord:ClientId"]!;
        options.ClientSecret = builder.Configuration["Discord:ClientSecret"]!;
        options.Scope.Add("identify");
        options.SaveTokens = true;

        // On login, grant the RosterAccess role only if the user holds the configured Discord role
        // in the guild. Users without it can still sign in but see no roster content (fail-closed).
        options.Events.OnCreatingTicket = async context =>
        {
            if (string.IsNullOrWhiteSpace(discordGuildId)
                || string.IsNullOrWhiteSpace(discordBotToken)
                || string.IsNullOrWhiteSpace(requiredRoleId))
                return;

            var userId = context.User.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(userId))
                return;

            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"https://discord.com/api/v10/guilds/{discordGuildId}/members/{userId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", discordBotToken);
            // Discord's API (behind Cloudflare) returns an empty-body 403 for requests without a
            // recognized User-Agent; the format is documented as "DiscordBot (url, version)".
            request.Headers.UserAgent.ParseAdd("DiscordBot (https://github.com/ZenBotCS, 1.0)");

            var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode)
                return; // not a guild member, or lookup failed → no access

            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
            var hasRole = json.RootElement.TryGetProperty("roles", out var roles)
                && roles.EnumerateArray().Any(r => r.GetString() == requiredRoleId);

            if (hasRole)
                context.Identity?.AddClaim(new Claim(ClaimTypes.Role, AuthRoles.RosterAccess));
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Behind the Caddy reverse proxy (which terminates TLS on the same host), honor the
// X-Forwarded-* headers so the app sees the real https scheme and external host. Without
// this the OAuth callback URL would be built as http://<internal> and Discord would reject it.
// Must run before HTTPS redirection and authentication. Loopback proxies are trusted by default.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Minimal login/logout endpoints (Blazor components can't issue auth challenges directly).
app.MapGet("/account/login", (string? returnUrl) =>
    Results.Challenge(
        new AuthenticationProperties { RedirectUri = returnUrl ?? "/", IsPersistent = true },
        [DiscordAuthenticationDefaults.AuthenticationScheme]));

app.MapPost("/account/logout", async (HttpContext context, string? returnUrl) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect(returnUrl ?? "/");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
