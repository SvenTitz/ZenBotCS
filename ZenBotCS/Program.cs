using CocApi.Cache;
using CocApi.Cache.Extensions;
using CocApi.Rest.Client;
using CocApi.Rest.Extensions;
using Discord;
using Discord.Addons.Hosting;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Globalization;
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

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .UseSerilog((hostingContext, loggerConfiguration) =>
            {
                loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration);
            })
            //.ConfigureLogging((hostingContext, logging) =>
            //{
            //    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
            //    logging.AddConsole();
            //    logging.AddDebug();
            //})
            .ConfigureDiscordHost((context, config) =>
            {
                config.SocketConfig = new DiscordSocketConfig
                {
                    LogLevel = LogSeverity.Info,
                    AlwaysDownloadUsers = true,
                    MessageCacheSize = 200,
                    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
                };
                config.Token = context.Configuration["DiscordToken"]!;
            })
            .ConfigureCocApi((context, services, options) =>
            {
                options.AddTokens(new ApiKeyToken(context.Configuration["CocApiToken"]!, ClientUtils.ApiKeyHeader.Authorization));
                options.AddCocApiHttpClients(
                    builder: builder => builder
                        .AddRetryPolicy(3)
                        .AddTimeoutPolicy(TimeSpan.FromMilliseconds(3000))
                        .AddCircuitBreakerPolicy(30, TimeSpan.FromSeconds(10))
                    );
            })
            .UseInteractionService((context, config) =>
            {
                config.LogLevel = LogSeverity.Info;
                config.UseCompiledLambda = true;
            })
            .ConfigureServices((context, services) =>
            {
                services.AddHostedService<InteractionHandler>();
                services.AddHostedService<DiscordLinkUpdateService>();
                services.AddHostedService<WarHistoryUpdateService>();
                services.AddTransient<TestService>();
                services.AddTransient<GspreadService>();
                services.AddTransient<PlayerService>();
                services.AddTransient<ClanService>();
                services.AddTransient<CwlService>();
                services.AddTransient<HelpService>();
                services.AddTransient<ClashKingApiClient>();
                services.AddTransient<EmbedHelper>();
                services.AddTransient<DiscordHelper>();
                services.AddDbContext<BotDataContext>(options =>
                    {
                        var connectionString = context.Configuration["BotDbConnectionString"]!;
                        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), b => b.MigrationsAssembly("ZenBotCS"));

                    });
                services.AddCocApiCache<CustomClansClient, PlayersClient, TimeToLiveProvider>(
                    dbContextOptions =>
                    {
                        var connectionString = context.Configuration["CocApiCacheConnectionString"]!;
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
            })
            .Build();

        await host.RunAsync();
    }
}