using System.Configuration;
using System.Reflection;
using Autofac;
using Autofac.Extras.Quartz;
using Config.Net;
using JavaJotter.Configuration.Interfaces;
using JavaJotter.Interfaces;
using JavaJotter.Services;
using JavaJotter.Services.Databases;
using SlackNet.Autofac;
using ILogger = JavaJotter.Interfaces.ILogger;

namespace JavaJotter;

public static class Program
{
    private static readonly CancellationTokenSource CancellationToken = new();
    private static IContainer? _container;

    private static bool _onlineMode = true;
    private static DbProviderEnum _databaseType = DbProviderEnum.Local;

    private static async Task Main(string[] args)
    {
        ParseCommandLineArgs(args);

        _container = BuildContainer();

        var logger = _container.Resolve<ILogger>();


        if (_onlineMode)
        {
            logger.Log("Connecting...");
            await _container.SlackServices().GetSocketModeClient().Connect();
            logger.Log("Connected. Waiting for events...");
        }
        else
        {
            logger.Log("Offline mode. Ready.");
        }


        var scheduler = _container.Resolve<IJobScheduler>();

        await scheduler.Start();
        
        await MaintainLoopUntilCancellationRequested(logger);

        await scheduler.Stop();
    }

    private static void ParseCommandLineArgs(string[] args)
    {
        _onlineMode = !args.Any(arg => string.Equals(arg, "--offline", StringComparison.OrdinalIgnoreCase));

        if (args.Any(arg => string.Equals(arg, "--postgres", StringComparison.OrdinalIgnoreCase)))
        {
            _databaseType = DbProviderEnum.Postgres;
        }
    }


    private static IAppAuthSettings RetrieveAuthSettings()
    {
        var settings = new ConfigurationBuilder<IAppAuthSettings>()
            .UseYamlFile("token.yaml").Build();

        return settings.OAuthToken == string.Empty
            ? throw new ConfigurationErrorsException("OAuthToken is empty. Please add it to token.yaml")
            : settings;
    }

    private static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();

        builder.RegisterType<ConsoleLoggingService>().As<ILogger>().SingleInstance();

        builder.RegisterModule(new QuartzAutofacFactoryModule());

        var settings = RetrieveAuthSettings();

        builder.Register(c => settings).As<IAppAuthSettings>().SingleInstance();


        if (_onlineMode)
        {
            builder.AddSlackNet(c => c.UseApiToken(settings.OAuthToken).UseAppLevelToken(settings.AppLevelToken)
                //Register our slack events here
                //   .RegisterEventHandler<MessageEvent, MessageHandler>()
            );

            builder.RegisterType<SlackScrappingService>().As<IMessageScrapper>();
            builder.RegisterType<SlackUsernameService>().As<IUsernameService>();
            builder.RegisterType<SlackChannelService>().As<IChannelService>();
        }
        else
        {
            builder.RegisterType<MockScrappingService>().As<IMessageScrapper>();
            builder.RegisterType<MockUsernameService>().As<IUsernameService>();
            builder.RegisterType<MockChannelService>().As<IChannelService>();
        }

        builder.RegisterType<RollFilter>().As<IRollFilter>();


        switch (_databaseType)
        {
            case DbProviderEnum.Local:
                builder.RegisterType<SqLiteDatabaseService>().As<IDatabaseConnection>();
                break;
            case DbProviderEnum.Postgres:
                builder.RegisterType<PostgresDatabaseService>().As<IDatabaseConnection>();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }


        builder.RegisterModule(new QuartzAutofacJobsModule(Assembly.GetExecutingAssembly()));
        
        builder.RegisterType<JobScheduler>().As<IJobScheduler>();
        
        return builder.Build();
    }


    private static async Task MaintainLoopUntilCancellationRequested(ILogger logger)
    {
        Console.CancelKeyPress += delegate(object? _, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            CancellationToken.Cancel();
        };

        try
        {
            await Task.Delay(-1, CancellationToken.Token);
        }
        catch (TaskCanceledException)
        {
            logger.Log("Exiting...");
        }
    }
}