using System.Data;
using System.Text;
using JavaJotter.Configuration.Interfaces;
using JavaJotter.Extensions;
using JavaJotter.Interfaces;
using JavaJotter.Types;
using Npgsql;

namespace JavaJotter.Services.Databases;

public class PostgresDatabaseService : IDatabaseConnection
{
    private readonly NpgsqlDataSource _dataSource;

    private bool _tablesCreated;

    public PostgresDatabaseService(IAppSettings appSettings)
    {
        _dataSource = NpgsqlDataSource.Create(
            appSettings.DatabaseConnectionString);

    }

    public async Task InsertRoll(Roll roll)
    {
        await CreateTables();

        const string sql = @"
            INSERT INTO usernames (slack_id, username)
            VALUES (@user_id, NULL)
            ON CONFLICT (slack_id) DO NOTHING;

            INSERT INTO channels (slack_id, channel_name)
            VALUES (@channel_id, NULL)
            ON CONFLICT (slack_id) DO NOTHING;

            INSERT INTO rolls (unix_milliseconds, channel_id, user_id, dice_value)
            VALUES (
                @unix_milliseconds,
                (SELECT id FROM channels WHERE slack_id = @channel_id),
                (SELECT id FROM usernames WHERE slack_id = @user_id),
                @dice_value
            )
            ON CONFLICT (unix_milliseconds, channel_id) DO NOTHING;
    ";


        var parameters = new Dictionary<string, object>
        {
            { "user_id", roll.UserId },
            { "channel_id", roll.ChannelId },
            { "unix_milliseconds", roll.DateTime.ToUnixTimeMilliseconds() },
            { "dice_value", roll.RollValue }
        };

        await ExecuteSqlWithParametersAsync(sql, parameters);
    }


    public async Task UpdateUsername(Username username)
    {
        await CreateTables();

        const string sql = @"
        INSERT INTO usernames (slack_id, username)
        VALUES (@slackId, @username)
        ON CONFLICT (slack_id) DO UPDATE
        SET username = EXCLUDED.username
        WHERE usernames.username IS DISTINCT FROM EXCLUDED.username OR usernames.username IS NULL;";

        var parameters = new Dictionary<string, object>
        {
            { "slackId", username.Id },
            { "username", username.Name ?? "" }
        };

        await ExecuteSqlWithParametersAsync(sql, parameters);
    }


    public async Task UpdateChannel(Channel channel)
    {
        await CreateTables();

        const string sql = @"
        INSERT INTO channels (slack_id, channel_name)
        VALUES (@slackId, @channel_name)
        ON CONFLICT (slack_id) DO UPDATE
        SET channel_name = EXCLUDED.channel_name
        WHERE channels.channel_name IS DISTINCT FROM EXCLUDED.channel_name OR channels.channel_name IS NULL;";

        var parameters = new Dictionary<string, object>
        {
            { "slackId", channel.Id },
            { "channel_name", channel.Name ?? "" }
        };

        await ExecuteSqlWithParametersAsync(sql, parameters);
    }


    public async Task<List<Username>> GetNullUsernames()
    {
        await CreateTables();

        const string sql = @"SELECT * FROM usernames WHERE username IS NULL";
        return await QueryDatabaseAsync(sql, reader =>
        {
            var id = reader["slack_id"] as string ?? "";
            var name = reader["username"] as string;
            return new Username(id, name);
        });
    }

    public async Task<List<Channel>> GetNullChannels()
    {
        await CreateTables();

        const string sql = @"SELECT * FROM channels WHERE channel_name IS NULL";
        return await QueryDatabaseAsync(sql, reader =>
        {
            var id = reader["slack_id"] as string ?? "";
            var name = reader["channel_name"] as string;
            return new Channel(id, name);
        });
    }


    public async Task<Roll?> GetLatestRoll()
    {
        await CreateTables();

        const string sql = @"
        SELECT rolls.unix_milliseconds, usernames.slack_id AS user_slack_id, channels.slack_id AS channel_slack_id, rolls.dice_value 
        FROM rolls 
        INNER JOIN usernames ON rolls.user_id = usernames.id
        INNER JOIN channels ON rolls.channel_id = channels.id
        ORDER BY rolls.unix_milliseconds DESC LIMIT 1;";

        return await GetRoll(sql);
    }

    public async Task<Roll?> GetEarliestRoll()
    {
        await CreateTables();

        const string sql = @"
        SELECT rolls.unix_milliseconds, usernames.slack_id AS user_slack_id, channels.slack_id AS channel_slack_id, rolls.dice_value 
        FROM rolls 
        INNER JOIN usernames ON rolls.user_id = usernames.id
        INNER JOIN channels ON rolls.channel_id = channels.id
        ORDER BY rolls.unix_milliseconds LIMIT 1;";

        return await GetRoll(sql);
    }

    private async Task<Roll?> GetRoll(string sql)
    {
        await using var command = _dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(reader.GetOrdinal("unix_milliseconds")))
            .DateTime;
        var userId = reader.GetString(reader.GetOrdinal("user_slack_id"));
        var channelId = reader.GetString(reader.GetOrdinal("channel_slack_id"));
        var value = reader.GetInt32(reader.GetOrdinal("dice_value"));

        return new Roll(dateTime, channelId, userId, value);
    }


    private async Task DeleteAllTablesAsync()
    {
        const string getTableNamesSql =
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public';";

        var tableNames = new List<string>();

        await using var getTableNamesCommand = _dataSource.CreateCommand(getTableNamesSql);
        await using var reader = await getTableNamesCommand.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var name = reader["table_name"].ToString();
            if (!string.IsNullOrEmpty(name))
                tableNames.Add(name);
        }

        if (tableNames.Count > 0)
        {
            var dropTablesSql = new StringBuilder("DROP TABLE IF EXISTS ");

            foreach (var tableName in tableNames) dropTablesSql.Append($"\"{tableName.Replace("\"", "\"\"")}\",");

            // Remove the trailing comma
            dropTablesSql.Length--;

            dropTablesSql.Append(';');

            await using var dropTablesCommand = _dataSource.CreateCommand(dropTablesSql.ToString());
            await dropTablesCommand.ExecuteNonQueryAsync();
        }
    }


    private async Task CreateTables()
    {
        if (_tablesCreated)
            return;

        var createTasks = new List<Task>
        {
            CreateUsernameTableIfNotExist(),
            CreateChannelTableIfNotExist()
        };

        await Task.WhenAll(createTasks);

        await CreateRollTableIfNotExist();

        _tablesCreated = true;
    }


    private async Task CreateUsernameTableIfNotExist()
    {
        const string sql = @"CREATE TABLE IF NOT EXISTS usernames (
                    id SERIAL PRIMARY KEY,
                    slack_id TEXT UNIQUE NOT NULL,
                    username TEXT);";

        await using var command = _dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateChannelTableIfNotExist()
    {
        const string sql = @"CREATE TABLE IF NOT EXISTS channels (
                                    id SERIAL PRIMARY KEY,
                                    slack_id TEXT UNIQUE NOT NULL,
                                    channel_name TEXT);";

        await using var command = _dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateRollTableIfNotExist()
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS rolls (
                unix_milliseconds BIGINT NOT NULL, 
                channel_id INTEGER NOT NULL,
                user_id INTEGER NOT NULL, 
                dice_value INTEGER NOT NULL,
                FOREIGN KEY (user_id) REFERENCES usernames(id),
                FOREIGN KEY (channel_id) REFERENCES channels(id),
                PRIMARY KEY (unix_milliseconds, channel_id)
            );

            CREATE INDEX IF NOT EXISTS rolls_user_id_index ON rolls (user_id);
            CREATE INDEX IF NOT EXISTS rolls_channel_id_index ON rolls (channel_id);
            CREATE INDEX IF NOT EXISTS rolls_unix_milliseconds_index ON rolls (unix_milliseconds);";

        await using var command = _dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<T>> QueryDatabaseAsync<T>(string sql, Func<IDataReader, T> mapFunction)
    {
        var resultList = new List<T>();

        await using var command = _dataSource.CreateCommand(sql);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync()) resultList.Add(mapFunction(reader));

        return resultList;
    }

    private async Task ExecuteSqlWithParametersAsync(string sql, Dictionary<string, object> parameters)
    {
        await using var command = _dataSource.CreateCommand(sql);
        foreach (var param in parameters) command.Parameters.AddWithValue(param.Key, param.Value);

        await command.ExecuteNonQueryAsync();
    }
}