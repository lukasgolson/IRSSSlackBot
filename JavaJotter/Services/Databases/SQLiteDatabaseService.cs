using System.Data;
using System.Data.SQLite;
using System.Text.RegularExpressions;
using JavaJotter.Extensions;
using JavaJotter.Interfaces;
using JavaJotter.Types;

namespace JavaJotter.Services.Databases;

public partial class SqLiteDatabaseService : IDatabaseConnection, IDisposable
{
    private readonly ILogger _logger;


    private SQLiteConnection? _sqLiteConnection;

    public SqLiteDatabaseService(ILogger logger)
    {
        _logger = logger;
    }


    public async Task InsertMessage(Message message)
    {
        if (_sqLiteConnection?.State != ConnectionState.Open)
            await Connect();
    }

    public async Task UpdateUsername(Username username)
    {
        if (_sqLiteConnection?.State != ConnectionState.Open) await Connect();

        const string sql = @"
        INSERT INTO usernames (slack_id, username)
        VALUES (@slackId, @username)
        ON CONFLICT(slack_id) DO UPDATE SET username = EXCLUDED.username;
    ";

        await using var command = new SQLiteCommand(sql, _sqLiteConnection);
        command.Parameters.AddWithValue("@slackId", username.Id);
        command.Parameters.AddWithValue("@username", username.Name);

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateChannel(Channel channel)
    {
        if (_sqLiteConnection?.State != ConnectionState.Open) await Connect();

        const string sql = @"
        INSERT OR IGNORE INTO channels (slack_id, channel_name)
        VALUES (@slackId, @channel_name)
        ON CONFLICT(slack_id) DO UPDATE SET channel_name = EXCLUDED.channel_name
    ";

        await using var command = new SQLiteCommand(sql, _sqLiteConnection);
        command.Parameters.AddWithValue("@slackId", channel.Id);
        command.Parameters.AddWithValue("@channel_name", channel.Name);

        await command.ExecuteNonQueryAsync();
    }

    public async Task InsertRoll(Roll roll)
    {
        if (_sqLiteConnection?.State != ConnectionState.Open)
            await Connect();

        const string insertUsernameSql = @"
        INSERT OR IGNORE INTO usernames (slack_id, username)
        VALUES (@user_id, NULL); 
    ";

        const string insertChannelSql = @"
        INSERT OR IGNORE INTO channels (slack_id, channel_name)
        VALUES (@channel_id, NULL); 
    ";

        const string insertRollSql = @"
        INSERT INTO rolls (unix_milliseconds, channel_id, user_id, dice_value)
        VALUES (
            @unix_milliseconds,
            (SELECT id FROM channels WHERE slack_id = @channel_id),
            (SELECT id FROM usernames WHERE slack_id = @user_id),
            @dice_value
        );
    ";

        // Insert username
        await using (var usernameCommand = new SQLiteCommand(insertUsernameSql, _sqLiteConnection))
        {
            usernameCommand.Parameters.AddWithValue("@user_id", roll.UserId);
            await usernameCommand.ExecuteNonQueryAsync();
        }

        // Insert channel
        await using (var channelCommand = new SQLiteCommand(insertChannelSql, _sqLiteConnection))
        {
            channelCommand.Parameters.AddWithValue("@channel_id", roll.ChannelId);
            await channelCommand.ExecuteNonQueryAsync();
        }

        // Insert roll
        await using (var rollCommand = new SQLiteCommand(insertRollSql, _sqLiteConnection))
        {
            rollCommand.Parameters.AddWithValue("@unix_milliseconds", roll.DateTime.ToUnixTimeMilliseconds());
            rollCommand.Parameters.AddWithValue("@channel_id", roll.ChannelId);
            rollCommand.Parameters.AddWithValue("@user_id", roll.UserId);
            rollCommand.Parameters.AddWithValue("@dice_value", roll.RollValue);
            await rollCommand.ExecuteNonQueryAsync();
        }
    }

    public async Task<Roll?> GetLatestRoll()
    {
        if (_sqLiteConnection?.State != ConnectionState.Open)
            await Connect();

        const string sql = @"SELECT * FROM rolls ORDER BY unix_milliseconds DESC LIMIT 1;";

        return await GetRoll(sql);
    }

    public async Task<Roll?> GetEarliestRoll()
    {
        if (_sqLiteConnection?.State != ConnectionState.Open)
            await Connect();

        const string sql = @"SELECT * FROM rolls ORDER BY unix_milliseconds LIMIT 1;";

        return await GetRoll(sql);
    }


    public async Task<List<Username>> GetNullUsernames()
    {
        if (_sqLiteConnection?.State != ConnectionState.Open)
            await Connect();

        const string sql = @"SELECT * FROM usernames WHERE username IS NULL";

        var nullUsernames = new List<Username>();

        await using var command = new SQLiteCommand(sql, _sqLiteConnection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var id = reader["slack_id"] as string ?? "";
            var name = reader["username"] as string ?? ""; // This will be null

            var username = new Username(id, name);

            nullUsernames.Add(username);
        }

        return nullUsernames;
    }

    public async Task<List<Channel>> GetNullChannels()
    {
        const string sql = @"SELECT * FROM channels WHERE channel_name IS NULL";

        var nullChannels = new List<Channel>();

        await using var command = new SQLiteCommand(sql, _sqLiteConnection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var id = reader["slack_id"] as string ?? "";
            var name = reader["channel_name"] as string ?? ""; // This will be null

            var channel = new Channel(id, name);

            nullChannels.Add(channel);
        }

        return nullChannels;
    }

    public void Dispose()
    {
        _sqLiteConnection?.Dispose();
    }

    private async Task<Roll?> GetRoll(string sql)
    {
        await using var command = new SQLiteCommand(sql, _sqLiteConnection);


        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(reader.GetOrdinal("unix_milliseconds")))
            .DateTime;
        var userId = reader.GetString(reader.GetOrdinal("user_id"));
        var value = reader.GetInt32(reader.GetOrdinal("dice_value"));

        return new Roll(dateTime, "", userId, value);
    }

    private async Task<Message> GetMessage(string sql)
    {
        await using var command = new SQLiteCommand(sql, _sqLiteConnection);

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            throw new Exception("No message found.");

        var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(reader.GetOrdinal("unix_milliseconds")))
            .DateTime;

        var channel = reader.GetString(reader.GetOrdinal("channel_id"));
        var userId = reader.GetString(reader.GetOrdinal("user_id"));
        var text = reader.GetString(reader.GetOrdinal("text"));

        return new Message(channel, dateTime, Guid.NewGuid(), userId, text, Array.Empty<string>());
    }

    private string? GetSqLiteVersion()
    {
        const string sql = @"SELECT SQLITE_VERSION()";

        using var versionCommand = new SQLiteCommand(sql, _sqLiteConnection);
        var version = versionCommand.ExecuteScalar().ToString();
        return version;
    }

    private void CreateTables(SQLiteConnection connection)
    {
        CreateUsernameTableIfNotExist(connection);
        CreateRollTableIfNotExist(connection);
        CreateChannelTableIfNotExist(connection);
        CreateMessageTableIfNotExist(connection);
    }

    private static void CreateUsernameTableIfNotExist(SQLiteConnection sqLiteConnection)
    {
        const string sql =
            @"CREATE TABLE IF NOT EXISTS usernames (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                slack_id TEXT UNIQUE NOT NULL,
                username TEXT NULL);";

        using var createUsernameTableCommand = new SQLiteCommand(sql, sqLiteConnection);
        createUsernameTableCommand.ExecuteNonQuery();
    }

    private static void CreateChannelTableIfNotExist(SQLiteConnection sqLiteConnection)
    {
        const string sql = @"CREATE TABLE IF NOT EXISTS channels (
                                id INTEGER PRIMARY KEY AUTOINCREMENT, 
                                slack_id TEXT UNIQUE NOT NULL, 
                                channel_name TEXT NULL);";

        using var createChannelTableCommand = new SQLiteCommand(sql, sqLiteConnection);
        createChannelTableCommand.ExecuteNonQuery();
    }

    private static void CreateRollTableIfNotExist(SQLiteConnection sqLiteConnection)
    {
        const string sql = @"
        CREATE TABLE IF NOT EXISTS rolls (
            unix_milliseconds INTEGER NOT NULL, 
            channel_id INTEGER NOT NULL,
            user_id INTEGER NOT NULL, 
            dice_value INTEGER NOT NULL,
            FOREIGN KEY (user_id) REFERENCES usernames(id),
            FOREIGN KEY (channel_id) REFERENCES channels(id),
            PRIMARY KEY (unix_milliseconds, channel_id)
    );
    CREATE INDEX IF NOT EXISTS unix_milliseconds_index ON rolls (unix_milliseconds);
";


        using var createRollTableCommand = new SQLiteCommand(sql, sqLiteConnection);
        createRollTableCommand.ExecuteNonQuery();
    }

    private static void CreateMessageTableIfNotExist(SQLiteConnection sqLiteConnection)
    {
        const string sql = """
                           
                                   CREATE TABLE IF NOT EXISTS rolls (
                                       unix_milliseconds INTEGER NOT NULL,
                                       channel_id INTEGER NOT NULL,
                                       user_id INTEGER NOT NULL,
                                       text TEXT NOT NULL,
                                       FOREIGN KEY (user_id) REFERENCES usernames(id),
                                       FOREIGN KEY (channel_id) REFERENCES channels(id),
                                       PRIMARY KEY (unix_milliseconds, channel_id)
                               );
                               CREATE INDEX IF NOT EXISTS unix_milliseconds_index ON rolls (unix_milliseconds);
                           """;


        using var createMessageTableCommand = new SQLiteCommand(sql, sqLiteConnection);
        createMessageTableCommand.ExecuteNonQuery();
    }


    private static void DeleteAllTables(SQLiteConnection sqLiteConnection)
    {
        const string getTableNamesSql =
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";

        var tableNames = new List<string>();

        using var command = new SQLiteCommand(getTableNamesSql, sqLiteConnection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader["name"].ToString();
            if (!string.IsNullOrWhiteSpace(name))
                tableNames.Add(name);
        }

        using var transaction = sqLiteConnection.BeginTransaction();
        try
        {
            foreach (var tableName in tableNames)
            {
                if (!IsValidIdentifier(tableName) || tableName.StartsWith("sqlite_"))
                    throw new ArgumentException($"Invalid or system table name: {tableName}");

                var deleteTableSql = $"DROP TABLE IF EXISTS \"{tableName}\";";
                using var dropTableCommand = new SQLiteCommand(deleteTableSql, sqLiteConnection);
                dropTableCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private Task Connect()
    {
        const string connectionString = "Data Source=db.sqlite";

        _sqLiteConnection = new SQLiteConnection(connectionString);

        _sqLiteConnection.Open();

        var version = GetSqLiteVersion();

        if (version != null) _logger.Log($"Opening connection with SQLite Database; version {version}");

#if DEBUG
        _logger.Log("Deleting all tables to ensure a clean start for testing in DEBUG mode.");
        DeleteAllTables(_sqLiteConnection);
#endif

        CreateTables(_sqLiteConnection);


        return Task.CompletedTask;
    }


    // For simplicity, we're just ensuring that the table name only contains alphanumeric characters and underscores
    private static bool IsValidIdentifier(string tableName)
    {
        return ValidateIdentity().IsMatch(tableName);
    }

    [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex ValidateIdentity();
}