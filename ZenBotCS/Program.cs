using System.Globalization;
using CocApi.Cache;
using CocApi.Cache.Extensions;
using CocApi.Rest.Client;
using CocApi.Rest.Extensions;
using Discord;
using Discord.Addons.Hosting;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using ZenBotCS.Clients;
using ZenBotCS.Entities;
using ZenBotCS.Handler;
using ZenBotCS.Helper;
using ZenBotCS.Services;
using ZenBotCS.Services.Background;
using ZenBotCS.Services.SlashCommands;

namespace ZenBotCS;

public class Program
{
    public static async Task Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");

        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        builder.Services.AddLogging(config =>
        {
            config.ClearProviders();

            Logger logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();

            config.AddSerilog(logger);
        });

        builder.Services.AddMemoryCache();

        builder.Services.AddDiscordHost((config, _) =>
        {
            config.SocketConfig = new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                AlwaysDownloadUsers = true,
                MessageCacheSize = 200,
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers
            };
            config.Token = builder.Configuration["DiscordToken"]!;
        });

        builder.Services.AddCocApi(options =>
        {
            options.AddTokens(new ApiKeyToken(builder.Configuration["CocApiToken"]!, ClientUtils.ApiKeyHeader.Authorization));
            options.AddCocApiHttpClients(
                builder: builder => builder
                    .AddRetryPolicy(3)
                    .AddTimeoutPolicy(TimeSpan.FromMilliseconds(3000))
                    .AddCircuitBreakerPolicy(30, TimeSpan.FromSeconds(10))
                );
        });

        builder.Services.AddInteractionService((config, _) =>
        {
            config.LogLevel = LogSeverity.Info;
            config.UseCompiledLambda = true;
            // Run commands synchronously so the per-interaction DI scope (InteractionHandler) stays
            // alive until the command finishes. With RunMode.Async the command runs on a background
            // task and the scope — and its BotDataContext — would be disposed mid-execution.
            config.DefaultRunMode = RunMode.Sync;
        });

        builder.Services
            .AddHostedService<InteractionHandler>()
            //.AddHostedService<ComponentHandler>()
            .AddHostedService<DiscordLinkUpdateService>()
            .AddHostedService<WarHistoryUpdateService>()
            .AddHostedService<PlayerStatsUpdateService>()
            .AddHostedService<LeadershipLogBackfillService>()
            .AddHostedService<CwlRosterReminderService>()
            .AddTransient<TestService>()
            .AddTransient<GspreadService>()
            .AddTransient<PlayerService>()
            .AddTransient<ClanService>()
            .AddTransient<CwlSignupWizardService>()
            .AddTransient<CwlDataService>()
            .AddTransient<CwlRolesService>()
            .AddTransient<CwlRosterService>()
            .AddTransient<CwlSignupService>()
            .AddTransient<HelpService>()
            .AddTransient<LinksService>()
            .AddTransient<ReminderService>()
            // Singleton: ClashKingApiClient owns one reused RestClient (and its HttpClient). As a
            // transient it created a new HttpClient per resolution, risking socket/handler exhaustion.
            // It's stateless and thread-safe, and depends only on ILogger, so a singleton is safe.
            .AddSingleton<ClashKingApiClient>()
            .AddTransient<ClashKingApiService>()
            .AddTransient<EmbedHelper>()
            .AddTransient<CwlSignupCache>()
            .AddTransient<DiscordHelper>()
            .AddTransient<UtilService>()
            .AddTransient<GatekeepService>();

        builder.Services.AddDbContext<BotDataContext>(options =>
        {
            var connectionString = builder.Configuration["BotDbConnectionString"]!;
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), b => b.MigrationsAssembly("ZenBotCS"));

        });
        builder.Services.AddCocApiCache<CustomClansClient, PlayersClient, TimeToLiveProvider>(
            dbContextOptions =>
            {
                var connectionString = builder.Configuration["CocApiCacheConnectionString"]!;
                dbContextOptions.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), b => b.MigrationsAssembly("ZenBotCS"));
                CacheDbContext dbContext = new((DbContextOptions<CacheDbContext>)dbContextOptions.Options);
            },
            options =>
            {
                options.ActiveWars.Enabled = true;
                options.ClanMembers.Enabled = true;
                options.Clans.Enabled = true;
                options.CwlWars.Enabled = true;
                options.NewCwlWars.Enabled = true;
                options.NewWars.Enabled = true;
                options.Players.Enabled = true;
                options.Wars.Enabled = true;
            });

        var host = builder.Build();

        await host.RunAsync();
    }
}