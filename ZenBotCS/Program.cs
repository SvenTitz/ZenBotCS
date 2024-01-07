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
using Microsoft.Extensions.Logging;
using ZenBotCS;
using ZenBotCS.Handler;
using ZenBotCS.Services;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                logging.AddConsole();
                logging.AddDebug();
            })
            .ConfigureDiscordHost((context, config) =>
            {
                config.SocketConfig = new DiscordSocketConfig
                {
                    LogLevel = LogSeverity.Info,
                    AlwaysDownloadUsers = false,
                    MessageCacheSize = 200,
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
                services.AddSingleton<TestService>();
                services.AddSingleton<GspreadService>();
                services.AddSingleton<PlayerService>();
                services.AddSingleton<ClanService>();
                services.AddSingleton<CwlService>();
                services.AddSingleton<ClashKingApiClient>();
                services.AddTransient<ClashKingApiService>();
                services.AddTransient<EmbedHelper>();
                services.AddCocApiCache<ClansClient, PlayersClient, TimeToLiveProvider>(
                        dbContextOptions =>
                        {
                            var connectionString = context.Configuration["ConnectionString"]!;
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
