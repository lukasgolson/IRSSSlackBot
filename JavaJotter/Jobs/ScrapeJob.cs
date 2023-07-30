using System.Diagnostics;
using JavaJotter.Interfaces;
using JavaJotter.Types;
using JetBrains.Annotations;
using Quartz;

namespace JavaJotter.Jobs;

[UsedImplicitly]
public class ScrapeJob : IJob
{
    public static readonly JobKey Key = new JobKey("scrape");


    private readonly ILogger _logger;
    private readonly IMessageScrapper _messageScrapper;
    private readonly IRollFilter _filter;
    private readonly IUsernameService _usernameService;
    private readonly IChannelService _channelService;
    private readonly IDatabaseConnection _databaseConnection;


    public ScrapeJob(ILogger logger, IMessageScrapper messageScrapper, IRollFilter filter,
        IUsernameService usernameService, IChannelService channelService, IDatabaseConnection databaseConnection)
    {
        _logger = logger;
        _messageScrapper = messageScrapper;
        _filter = filter;
        _usernameService = usernameService;
        _channelService = channelService;
        _databaseConnection = databaseConnection;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.Log("Scraping");
        var lastRoll = await _databaseConnection.GetLatestRoll();

        var lastScrape = lastRoll?.DateTime;

        _logger.Log(
            $"Last scrape: {(lastScrape.HasValue ? lastScrape.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Never")}");




        int rollCounter = 0;
        await foreach (var message in _messageScrapper.Scrape(lastScrape))
        {
            var roll = _filter.ProcessMessage(message);
            if (roll == null)
            {
                continue;
            }

            rollCounter++;
            await _databaseConnection.InsertRoll(roll);
        }
      


        _logger.Log($"Found and added {rollCounter} rolls to database.");

     

        var nullUsernames = await _databaseConnection.GetNullUsernames();

        var nullUsernamesReconciled = 0;

        if (nullUsernames.Count > 0)
        {
            _logger.Log($"Found {nullUsernames.Count} usernames needing identifier reconciliation. Fixing...");

            foreach (var username in nullUsernames)
            {
                var user = await _usernameService.GetUsername(username.Id);
                if (user == null) continue;
                await _databaseConnection.UpdateUsername(user);
                nullUsernamesReconciled++;
            }
        }


        var nullChannels = await _databaseConnection.GetNullChannels();

        var nullChannelsReconciled = 0;

        if (nullChannels.Count > 0)
        {
            _logger.Log($"Found {nullChannels.Count} channels needing identifier reconciliation. Fixing...");


            foreach (var channel in nullChannels)
            {
                var channelInfo = await _channelService.GetChannel(channel.Id);
                if (channelInfo == null) continue;
                await _databaseConnection.UpdateChannel(channelInfo);
                nullChannelsReconciled++;
            }
        }

        var completeString = "Done scraping.";

        if (nullUsernames.Count > 0)
        {
            completeString += $" {nullUsernamesReconciled} usernames reconciled.";
        }

        if (nullChannels.Count > 0)
        {
            completeString += $" {nullChannelsReconciled} channels reconciled.";
        }

        _logger.Log(completeString);
    }
}