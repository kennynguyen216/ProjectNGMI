using Microsoft.Data.Sqlite;


class Database
{
    private const string ConnectionString = "Data source = memory.db";
    public static void Initialize()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Analysis (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Code TEXT NOT NULL,
                Analysis TEXT NOT NULL, 
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
            );";
        command.ExecuteNonQuery();
    }

    public static bool IsAlreadyAnalyzed(string code)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Analysis WHERE code = $code";
        command.Parameters.AddWithValue("$code",code);
        return (long)command.ExecuteScalar()! >0;
    }

    public static void SaveAnalysis(string code, string analysis)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Analysis (Code, Analysis) VALUES ($code, $analysis)";
        command.Parameters.AddWithValue("$code",code);
        command.Parameters.AddWithValue("$analysis", analysis);
        command.ExecuteNonQuery();
    }

    public static string GetPastAnalyses()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT code FROM Analysis";
        var results = new List<string>();
        using var reader = command.ExecuteReader();
        while(reader.Read())
        {
            results.Add(reader.GetString(0));
        }
        return string.Join("\n", results);

    }

}