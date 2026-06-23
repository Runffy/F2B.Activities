using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using SpiderAgent.App.Services;
using SpiderAgent.Core.Recording;

namespace SpiderAgent.App.Storage;

public sealed class SqliteSessionDatabase : ISessionDatabase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AppPaths _paths;
    private readonly string _connectionString;

    public SqliteSessionDatabase(AppPaths paths)
    {
        _paths = paths;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS workspace_sessions (
                session_id TEXT PRIMARY KEY,
                title TEXT NOT NULL DEFAULT '',
                first_prompt TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                has_recording INTEGER NOT NULL DEFAULT 0,
                has_analysis_history INTEGER NOT NULL DEFAULT 0,
                last_output_script_path TEXT,
                output_log_text TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS chat_messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES workspace_sessions(session_id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_chat_messages_session
                ON chat_messages(session_id, ordinal);
            """, cancellationToken);

        await MigrateLegacyJsonAsync(connection, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceSessionMetadata>> ListSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);

        var results = new List<WorkspaceSessionMetadata>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT session_id, title, first_prompt, created_at, updated_at,
                   has_recording, has_analysis_history, last_output_script_path
            FROM workspace_sessions
            ORDER BY updated_at DESC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadMetadata(reader));
        }

        return results;
    }

    public async Task<WorkspaceSessionMetadata?> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT session_id, title, first_prompt, created_at, updated_at,
                   has_recording, has_analysis_history, last_output_script_path
            FROM workspace_sessions
            WHERE session_id = $id;
            """;
        command.Parameters.AddWithValue("$id", sessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadMetadata(reader) : null;
    }

    public async Task SaveSessionAsync(
        WorkspaceSessionMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO workspace_sessions (
                session_id, title, first_prompt, created_at, updated_at,
                has_recording, has_analysis_history, last_output_script_path
            ) VALUES (
                $id, $title, $firstPrompt, $createdAt, $updatedAt,
                $hasRecording, $hasAnalysis, $lastScript
            )
            ON CONFLICT(session_id) DO UPDATE SET
                title = excluded.title,
                first_prompt = excluded.first_prompt,
                updated_at = excluded.updated_at,
                has_recording = excluded.has_recording,
                has_analysis_history = excluded.has_analysis_history,
                last_output_script_path = excluded.last_output_script_path;
            """;

        BindMetadata(command, metadata);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM workspace_sessions WHERE session_id = $id;";
        command.Parameters.AddWithValue("$id", sessionId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        var outputDir = _paths.GetSessionOutputDirectory(sessionId);
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }

    public async Task SaveChatHistoryAsync(
        string sessionId,
        IReadOnlyList<PersistedChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM chat_messages WHERE session_id = $id;";
            deleteCommand.Parameters.AddWithValue("$id", sessionId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        for (var i = 0; i < messages.Count; i++)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO chat_messages (session_id, ordinal, role, content)
                VALUES ($sessionId, $ordinal, $role, $content);
                """;
            insertCommand.Parameters.AddWithValue("$sessionId", sessionId);
            insertCommand.Parameters.AddWithValue("$ordinal", i);
            insertCommand.Parameters.AddWithValue("$role", messages[i].Role);
            insertCommand.Parameters.AddWithValue("$content", messages[i].Content);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PersistedChatMessage>?> LoadChatHistoryAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT role, content
            FROM chat_messages
            WHERE session_id = $id
            ORDER BY ordinal;
            """;
        command.Parameters.AddWithValue("$id", sessionId);

        var messages = new List<PersistedChatMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(new PersistedChatMessage
            {
                Role = reader.GetString(0),
                Content = reader.GetString(1)
            });
        }

        return messages.Count == 0 ? null : messages;
    }

    public async Task SaveOutputLogTextAsync(
        string sessionId,
        string text,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO workspace_sessions (session_id, output_log_text, updated_at, created_at, title)
            VALUES ($id, $text, $updatedAt, $updatedAt, '')
            ON CONFLICT(session_id) DO UPDATE SET
                output_log_text = excluded.output_log_text,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));
        command.Parameters.AddWithValue("$id", sessionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string> LoadOutputLogTextAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT output_log_text FROM workspace_sessions WHERE session_id = $id;
            """;
        command.Parameters.AddWithValue("$id", sessionId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is string text ? text : string.Empty;
    }

    private async Task MigrateLegacyJsonAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_paths.LegacySessionsRoot))
        {
            return;
        }

        foreach (var sessionDir in Directory.EnumerateDirectories(_paths.LegacySessionsRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sessionId = Path.GetFileName(sessionDir);
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                continue;
            }

            if (await SessionExistsAsync(connection, sessionId, cancellationToken))
            {
                continue;
            }

            WorkspaceSessionMetadata? metadata = null;
            var workspacePath = Path.Combine(sessionDir, "workspace.json");
            if (File.Exists(workspacePath))
            {
                await using var stream = File.OpenRead(workspacePath);
                metadata = await JsonSerializer.DeserializeAsync<WorkspaceSessionMetadata>(
                    stream, JsonOptions, cancellationToken);
            }

            if (metadata is null)
            {
                var recordingPath = Path.Combine(sessionDir, "session.json");
                if (!File.Exists(recordingPath))
                {
                    continue;
                }

                metadata = new WorkspaceSessionMetadata
                {
                    SessionId = sessionId,
                    Title = sessionId,
                    CreatedAt = DateTimeOffset.Now,
                    UpdatedAt = DateTimeOffset.Now,
                    HasRecording = true
                };
            }

            await SaveSessionAsync(metadata, cancellationToken);

            var chatPath = Path.Combine(sessionDir, "chat-history.json");
            if (File.Exists(chatPath))
            {
                await using var stream = File.OpenRead(chatPath);
                var chat = await JsonSerializer.DeserializeAsync<List<PersistedChatMessage>>(
                    stream, JsonOptions, cancellationToken);
                if (chat is { Count: > 0 })
                {
                    await SaveChatHistoryAsync(sessionId, chat, cancellationToken);
                }
            }
        }
    }

    private static async Task<bool> SessionExistsAsync(
        SqliteConnection connection,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM workspace_sessions WHERE session_id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", sessionId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    private static WorkspaceSessionMetadata ReadMetadata(SqliteDataReader reader)
        => new()
        {
            SessionId = reader.GetString(0),
            Title = reader.GetString(1),
            FirstPrompt = reader.IsDBNull(2) ? null : reader.GetString(2),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(3)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(4)),
            HasRecording = reader.GetInt64(5) != 0,
            HasAnalysisHistory = reader.GetInt64(6) != 0,
            LastOutputScriptPath = reader.IsDBNull(7) ? null : reader.GetString(7)
        };

    private static void BindMetadata(SqliteCommand command, WorkspaceSessionMetadata metadata)
    {
        command.Parameters.AddWithValue("$id", metadata.SessionId);
        command.Parameters.AddWithValue("$title", metadata.Title);
        command.Parameters.AddWithValue("$firstPrompt", (object?)metadata.FirstPrompt ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", metadata.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", metadata.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$hasRecording", metadata.HasRecording ? 1 : 0);
        command.Parameters.AddWithValue("$hasAnalysis", metadata.HasAnalysisHistory ? 1 : 0);
        command.Parameters.AddWithValue("$lastScript", (object?)metadata.LastOutputScriptPath ?? DBNull.Value);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private SqliteConnection OpenConnection() => new(_connectionString);
}
