using BackendAPI.Models;
using Npgsql;

namespace BackendAPI.Services
{
    public class SupabaseIdempotencyService
    {
        private readonly IConfiguration _configuration;

        public SupabaseIdempotencyService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string ConnectionString =>
            _configuration.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException("Missing Supabase connection string");

        public string BuildKey(ReplyCommand command)
        {
            return $"{command.EventId}:{command.CommentId}:{command.Action}";
        }

        public async Task<bool> TryStartAsync(
            ReplyCommand command,
            CancellationToken cancellationToken = default)
        {
            var key = BuildKey(command);

            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                INSERT INTO idempotency_keys
                (
                    idempotency_key,
                    command_id,
                    event_id,
                    comment_id,
                    action,
                    status
                )
                VALUES
                (
                    @key,
                    @commandId,
                    @eventId,
                    @commentId,
                    @action,
                    'processing'
                )
                ON CONFLICT (idempotency_key) DO NOTHING;
            ";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("key", key);
            cmd.Parameters.AddWithValue("commandId", command.CommandId);
            cmd.Parameters.AddWithValue("eventId", command.EventId);
            cmd.Parameters.AddWithValue("commentId", command.CommentId ?? "");
            cmd.Parameters.AddWithValue("action", command.Action);

            var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);

            return rows > 0;
        }

        public async Task MarkProcessedAsync(
            ReplyCommand command,
            CancellationToken cancellationToken = default)
        {
            var key = BuildKey(command);

            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                UPDATE idempotency_keys
                SET status = 'processed'
                WHERE idempotency_key = @key;
            ";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("key", key);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task RemoveAsync(
            ReplyCommand command,
            CancellationToken cancellationToken = default)
        {
            var key = BuildKey(command);

            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                DELETE FROM idempotency_keys
                WHERE idempotency_key = @key;
            ";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("key", key);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}