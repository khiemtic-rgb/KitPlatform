using System.Text.Json;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Npgsql;

namespace KitPlatform.Packs.PersonalIntelligence.PaperHost;

public sealed class FilePaperOperationLog : IPaperOperationLog
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly Dictionary<string, PaperOperationReceipt> _rows;

    private FilePaperOperationLog(string path, Dictionary<string, PaperOperationReceipt> rows)
    {
        _path = path;
        _rows = rows;
    }

    public static FilePaperOperationLog Load(string path)
    {
        var rows = new Dictionary<string, PaperOperationReceipt>(StringComparer.Ordinal);
        if (File.Exists(path))
        {
            var existing = JsonSerializer.Deserialize<List<PaperOperationReceipt>>(File.ReadAllText(path), JsonOptions) ?? [];
            foreach (var row in existing)
                rows[row.OperationKey] = row;
        }

        return new FilePaperOperationLog(path, rows);
    }

    public PaperOperationReceipt? Find(string operationKey)
        => _rows.TryGetValue(operationKey, out var row) ? row : null;

    public bool TryAdd(PaperOperationReceipt receipt)
        => _rows.TryAdd(receipt.OperationKey, receipt);

    public void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(_rows.Values.ToList(), JsonOptions);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, json + Environment.NewLine);
        File.Move(temporary, _path, overwrite: true);
    }
}

public sealed class SqlPaperOperationLog : IPaperOperationLog, IDisposable
{
    private readonly NpgsqlConnection _connection;

    public SqlPaperOperationLog(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The connection file is empty.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, "trading_agent", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The paper host accepts only database trading_agent and refuses database {builder.Database}.");
        _connection = new NpgsqlConnection(builder.ConnectionString);
        _connection.Open();
    }

    public PaperOperationReceipt? Find(string operationKey)
    {
        using var command = new NpgsqlCommand(
            """
            SELECT payload_hash, status, result
            FROM pack_personal_intelligence.paper_host_receipt
            WHERE operation_key = @key
            """,
            _connection);
        command.Parameters.AddWithValue("key", operationKey);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;
        return new PaperOperationReceipt(operationKey, reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    public bool TryAdd(PaperOperationReceipt receipt)
    {
        using var command = new NpgsqlCommand(
            """
            INSERT INTO pack_personal_intelligence.paper_host_receipt
                (operation_key, payload_hash, status, result)
            VALUES (@key, @hash, @status, @result)
            ON CONFLICT (operation_key) DO NOTHING
            """,
            _connection);
        command.Parameters.AddWithValue("key", receipt.OperationKey);
        command.Parameters.AddWithValue("hash", receipt.PayloadHash);
        command.Parameters.AddWithValue("status", receipt.Status);
        command.Parameters.AddWithValue("result", receipt.Result);
        return command.ExecuteNonQuery() == 1;
    }

    public void Dispose() => _connection.Dispose();
}
