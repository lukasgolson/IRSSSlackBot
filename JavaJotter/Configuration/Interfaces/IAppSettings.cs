namespace JavaJotter.Configuration.Interfaces;

public interface IAppSettings
{
    public string OAuthToken { get; }
    public string AppLevelToken { get; }

    string DatabaseConnectionString { get; set; }
    

    string HistoricRangeYears { get; set; }
    string ScrapeCronExpression { get; }
}