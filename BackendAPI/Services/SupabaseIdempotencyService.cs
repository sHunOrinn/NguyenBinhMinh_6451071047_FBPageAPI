using System.Collections.Concurrent;
using BackendAPI.Models;
using Npgsql;

namespace BackendAPI.Services
{
    public class SupabaseIdempotencyService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SupabaseIdempotencyService> _logger;

        // Fallback chống trùng trong RAM nếu Supabase bị timeout
        private readonly ConcurrentDictionary<string, string> _memoryKeys = new();

        public SupabaseIdempotencyService(
            IConfiguration configuration,
            ILogger<SupabaseIdempotencyService> logger)
        {
            _configuration = configuration;
            _logger = logger;
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

            // Chặn trùng ngay bằng RAM trước
            if (!_memoryKeys.TryAdd(key, "processing"))
            {
                return false;
            }

            try
            {
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
                cmd.CommandTimeout = 15;

                cmd.Parameters.AddWithValue("key", key);
                cmd.Parameters.AddWithValue("commandId", command.CommandId ?? "");
                cmd.Parameters.AddWithValue("eventId", command.EventId ?? "");
                cmd.Parameters.AddWithValue("commentId", command.CommentId ?? "");
                cmd.Parameters.AddWithValue("action", command.Action ?? "");

                var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);

                if (rows <= 0)
                {
                    _logger.LogWarning(
                        "Duplicate command detected from Supabase. Key: {Key}",
                        key);

                    _memoryKeys.TryRemove(key, out _);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                // Không để Supabase timeout làm chết Backend.
                // RAM vẫn đang giữ key nên vẫn chống trùng trong phiên chạy hiện tại.
                _logger.LogError(
                    ex,
                    "Supabase TryStartAsync failed but memory idempotency is active. Key: {Key}",
                    key);

                return true;
            }
        }

        public async Task MarkProcessedAsync(
            ReplyCommand command,
            CancellationToken cancellationToken = default)
        {
            var key = BuildKey(command);

            _memoryKeys[key] = "processed";

            try
            {
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
                        'processed'
                    )
                    ON CONFLICT (idempotency_key)
                    DO UPDATE SET
                        status = 'processed';
                ";

                await using var cmd = new NpgsqlCommand(sql, connection);
                cmd.CommandTimeout = 15;

                cmd.Parameters.AddWithValue("key", key);
                cmd.Parameters.AddWithValue("commandId", command.CommandId ?? "");
                cmd.Parameters.AddWithValue("eventId", command.EventId ?? "");
                cmd.Parameters.AddWithValue("commentId", command.CommentId ?? "");
                cmd.Parameters.AddWithValue("action", command.Action ?? "");

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Supabase MarkProcessedAsync failed but ignored. Key: {Key}",
                    key);
            }
        }

        public async Task RemoveAsync(
            ReplyCommand command,
            CancellationToken cancellationToken = default)
        {
            var key = BuildKey(command);

            // Cho phép retry lại nếu command lỗi thật
            _memoryKeys.TryRemove(key, out _);

            try
            {
                await using var connection = new NpgsqlConnection(ConnectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    DELETE FROM idempotency_keys
                    WHERE idempotency_key = @key;
                ";

                await using var cmd = new NpgsqlCommand(sql, connection);
                cmd.CommandTimeout = 15;

                cmd.Parameters.AddWithValue("key", key);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Supabase RemoveAsync failed but ignored. Key: {Key}",
                    key);
            }
        }
    }
}