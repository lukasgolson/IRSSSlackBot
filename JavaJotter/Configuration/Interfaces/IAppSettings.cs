namespace JavaJotter.Configuration.Interfaces;

public interface IAppSettings
{
    public string OAuthToken { get; }
    public string SigningSecret { get; }
    public string AppLevelToken { get; }
    string DatabaseHost { get; }
    string DatabasePort { get; }
    string DatabaseUsername { get; }
    string DatabasePassword { get; }
    string Database { get; }
    string HistoricRangeYears { get; set; }
    string ScrapeCronExpression { get; }
}