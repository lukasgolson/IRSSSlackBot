using JavaJotter.Configuration.Interfaces;
using JavaJotter.Interfaces;
using JavaJotter.Jobs;
using Quartz;
using Quartz.Util;

namespace JavaJotter.Services;

public class JobScheduler : IJobScheduler
{
    private readonly IScheduler _scheduler;
    private readonly ILogger _logger;
    private readonly string _schedulingCronExpression;

    public JobScheduler(IScheduler scheduler, ILogger logger, IAppSettings appSettings)
    {
        _scheduler = scheduler;
        _logger = logger;
        _schedulingCronExpression = appSettings.ScrapeCronExpression;

        _schedulingCronExpression = string.IsNullOrWhiteSpace(appSettings.ScrapeCronExpression) ? "0 0 9,17 ? * *" : appSettings.ScrapeCronExpression;
    }

    public async Task Start()
    {
        _logger.Log("Starting scheduler...");
        await _scheduler.Start();

        var scrapeJob = JobBuilder.Create<ScrapeJob>()
            .WithIdentity("ScrapeMessages")
            .Build();

        var scrapeTrigger = TriggerBuilder.Create()
            .WithIdentity("ScrapeMessagesTrigger")
            .StartNow()
            .WithCronSchedule(_schedulingCronExpression, x => x.InTimeZone(TimeZoneInfo.Local))
            .Build();


        await _scheduler.ScheduleJob(scrapeJob, scrapeTrigger);

        _logger.Log("Scheduled scraping with cron expression: " + _schedulingCronExpression);

        await _scheduler.TriggerJob(scrapeJob.Key);
    }

    public async Task Stop()
    {
        await _scheduler.Shutdown();
    }
}