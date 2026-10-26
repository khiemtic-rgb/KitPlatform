using System.Text.Json;
using Npgsql;

namespace KitPlatform.Packs.PersonalIntelligence.ResearchDashboard;

public interface IResearchReader
{
    ResearchReadBundle Read();
}

public sealed class FixtureResearchReader : IResearchReader
{
    private readonly string _path;

    public FixtureResearchReader(string path) => _path = path;

    public ResearchReadBundle Read()
    {
        var json = File.ReadAllText(_path);
        var file = JsonSerializer.Deserialize<FixtureFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Research fixture is empty.");
        return new ResearchReadBundle(
            file.CandleCount ?? file.Candles.Count,
            file.Candles,
            file.Signals,
            file.Decisions,
            file.Fills,
            file.Runs,
            file.Sessions);
    }

    private sealed class FixtureFile
    {
        public int? CandleCount { get; set; }
        public List<CandleInput> Candles { get; set; } = [];
        public List<SignalInput> Signals { get; set; } = [];
        public List<DecisionInput> Decisions { get; set; } = [];
        public List<FillInput> Fills { get; set; } = [];
        public List<RunInput> Runs { get; set; } = [];
        public List<SessionInput> Sessions { get; set; } = [];
    }
}

public sealed class SqlResearchReader : IResearchReader, IDisposable
{
    private readonly NpgsqlConnection _connection;

    public SqlResearchReader(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The research dashboard connection is empty.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, "trading_agent", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The research dashboard accepts only database trading_agent and refuses database {builder.Database}.");
        _connection = new NpgsqlConnection(builder.ConnectionString);
        _connection.Open();
    }

    public ResearchReadBundle Read()
    {
        var count = ExecuteCount(ResearchReadSql.CandleCount);
        var candles = new List<CandleInput>();
        using (var command = new NpgsqlCommand(ResearchReadSql.Candles, _connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                candles.Add(new CandleInput(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetFieldValue<DateTimeOffset>(2),
                    reader.GetFieldValue<DateTimeOffset>(3),
                    reader.GetDecimal(4),
                    reader.GetDecimal(5),
                    reader.GetDecimal(6),
                    reader.GetDecimal(7),
                    reader.GetFieldValue<DateTimeOffset>(8)));
            }
        }

        var signals = new List<SignalInput>();
        using (var command = new NpgsqlCommand(ResearchReadSql.Signals, _connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                signals.Add(new SignalInput(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetFieldValue<DateTimeOffset>(3),
                    reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                    reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    reader.GetString(6)));
            }
        }

        var decisions = new List<DecisionInput>();
        using (var command = new NpgsqlCommand(ResearchReadSql.Decisions, _connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                decisions.Add(new DecisionInput(reader.GetString(0), reader.GetBoolean(1), reader.GetString(2)));
        }

        var fills = new List<FillInput>();
        using (var command = new NpgsqlCommand(ResearchReadSql.Fills, _connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                fills.Add(new FillInput(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetDecimal(2),
                    reader.GetDecimal(3),
                    reader.GetDecimal(4),
                    reader.GetDecimal(5),
                    reader.GetDecimal(6),
                    reader.GetDecimal(7)));
            }
        }

        var runs = new List<RunInput>();
        using (var command = new NpgsqlCommand(ResearchReadSql.Runs, _connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                runs.Add(new RunInput(reader.GetInt64(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2), reader.GetString(3)));
        }

        var sessions = new List<SessionInput>();
        using (var command = new NpgsqlCommand(ResearchReadSql.Sessions, _connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var result = reader.GetString(2);
                sessions.Add(new SessionInput(reader.GetFieldValue<DateTimeOffset>(0), reader.GetString(1), ResearchReadModel.ReadPnl(result), ResearchReadModel.ReadPnlStatus(result)));
            }
        }

        return new ResearchReadBundle(count, candles, signals, decisions, fills, runs, sessions);
    }

    private int ExecuteCount(string sql)
    {
        using var command = new NpgsqlCommand(sql, _connection);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Dispose() => _connection.Dispose();
}

public static class DashboardConnection
{
    public static string ReadFile(string path)
    {
        var full = Path.GetFullPath(path).Replace('\\', '/');
        if (full.Contains("/etc/kit-platform/api.env", StringComparison.OrdinalIgnoreCase)
            || full.EndsWith("/api.env", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The research dashboard does not read the production API environment.");
        var text = File.ReadAllText(path).Trim();
        var builder = new NpgsqlConnectionStringBuilder(text);
        if (!string.Equals(builder.Database, "trading_agent", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The research dashboard accepts only database trading_agent and refuses database {builder.Database}.");
        return builder.ConnectionString;
    }
}
