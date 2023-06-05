using JavaJotter.Interfaces;
using JavaJotter.Jobs;
using Quartz;

namespace JavaJotter.Services;

public class JobScheduler : IJobScheduler
{
    private readonly IScheduler _scheduler;
    private readonly ILogger _logger;

    public JobScheduler(IScheduler scheduler, ILogger logger)
    {
        _scheduler = scheduler;
        _logger = logger;
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
            .WithCronSchedule("0 0 9,17 ? * *", x => x.InTimeZone(TimeZoneInfo.Local))
            .Build();


        await _scheduler.ScheduleJob(scrapeJob, scrapeTrigger);

        _logger.Log("Scheduled scraping");

        await _scheduler.TriggerJob(scrapeJob.Key);
    }

    public async Task Stop()
    {
        await _scheduler.Shutdown();
    }
}