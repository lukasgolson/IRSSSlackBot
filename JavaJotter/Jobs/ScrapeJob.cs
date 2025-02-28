﻿using JavaJotter.Configuration.Interfaces;
using JavaJotter.Interfaces;
using JavaJotter.Types;
using JetBrains.Annotations;
using Quartz;

namespace JavaJotter.Jobs;

[UsedImplicitly]
public class ScrapeJob : IJob
{
    private readonly IAppSettings _appSettings;
    private readonly IChannelService _channelService;
    private readonly IDatabaseConnection _databaseConnection;
    private readonly IRollFilter _filter;
    private readonly ILogger _logger;
    private readonly IMessageScrapper _messageScrapper;
    private readonly IUsernameService _usernameService;


    public ScrapeJob(ILogger logger, IMessageScrapper messageScrapper, IRollFilter filter,
        IUsernameService usernameService, IChannelService channelService, IDatabaseConnection databaseConnection,
        IAppSettings appSettings)
    {
        _logger = logger;
        _messageScrapper = messageScrapper;
        _filter = filter;
        _usernameService = usernameService;
        _channelService = channelService;
        _databaseConnection = databaseConnection;
        _appSettings = appSettings;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.Log("Scraping");
        var latestRoll = await _databaseConnection.GetLatestRoll();
        var earliestRoll = await _databaseConnection.GetEarliestRoll();


        var lastScrape = latestRoll?.DateTime;
        var earliestScrape = earliestRoll?.DateTime;

        _logger.Log(
            $"Last scrape: {(lastScrape.HasValue ? lastScrape.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Never")}");


        if (!int.TryParse(_appSettings.HistoricRangeYears, out var backtraceNumberOfYears))
        {
            _logger.LogWarning(
                $"Config file {nameof(_appSettings.HistoricRangeYears)} not set to a valid integer, defaulting value to 2.");
            backtraceNumberOfYears = 2;
        }

        _logger.Log($"Backtracing {backtraceNumberOfYears} years");

        var historicCounter = 0;
        var historicRange = DateTime.Today.AddYears(-backtraceNumberOfYears);
        if (earliestScrape?.Date > historicRange.Date)
        {
            _logger.Log($"Scraping historic rolls from {historicRange} to {earliestScrape}");
            await foreach (var message in _messageScrapper.Scrape(historicRange, earliestScrape))
            {
                historicCounter++;
                await ProcessMessage(message);
            }

            _logger.Log($"Captured {historicCounter} historic rolls...");
        }


        _logger.Log($"Scraping current rolls from {lastScrape} to {DateTime.Now}");

        var currentCounter = 0;
        await foreach (var message in _messageScrapper.Scrape(lastScrape, DateTime.Now))
        {
            currentCounter++;
            await ProcessMessage(message);
        }

        _logger.Log($"Captured {currentCounter} current rolls...");

        _logger.Log($"Found and added {currentCounter + historicCounter} total rolls to the database.");


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

    private async Task ProcessMessage(Message message)
    {
        var roll = _filter.ExtractRoll(message);
        if (roll == null)
        {
            return;
        }

        await _databaseConnection.InsertRoll(roll);
    }
}