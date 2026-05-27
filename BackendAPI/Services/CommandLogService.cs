using BackendAPI.Models;
using Npgsql;

namespace BackendAPI.Services
{
    public class CommandLogService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<CommandLogService> _logger;

        public CommandLogService(
            IConfiguration configuration,
            ILogger<CommandLogService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private string ConnectionString =>
            _configuration.GetConnectionString("Supabase")
            ?? throw new InvalidOperationException("Missing Supabase connection string");

        public async Task SaveAsync(
            ReplyCommand command,
            string status,
            string? errorMessage = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new NpgsqlConnection(ConnectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    INSERT INTO command_logs
                    (
                        command_id,
                        event_id,
                        comment_id,
                        action,
                        reply_text,
                        intent,
                        sentiment,
                        status,
                        retry_count,
                        error_message
                    )
                    VALUES
                    (
                        @commandId,
                        @eventId,
                        @commentId,
                        @action,
                        @replyText,
                        @intent,
                        @sentiment,
                        @status,
                        @retryCount,
                        @errorMessage
                    );
                ";

                await using var cmd = new NpgsqlCommand(sql, connection);
                cmd.CommandTimeout = 15;

                cmd.Parameters.AddWithValue("commandId", command.CommandId ?? "");
                cmd.Parameters.AddWithValue("eventId", command.EventId ?? "");
                cmd.Parameters.AddWithValue("commentId", command.CommentId ?? "");
                cmd.Parameters.AddWithValue("action", command.Action ?? "");
                cmd.Parameters.AddWithValue("replyText", command.ReplyText ?? "");
                cmd.Parameters.AddWithValue("intent", command.Intent ?? "unknown");
                cmd.Parameters.AddWithValue("sentiment", command.Sentiment ?? "neutral");
                cmd.Parameters.AddWithValue("status", status);
                cmd.Parameters.AddWithValue("retryCount", command.RetryCount);
                cmd.Parameters.AddWithValue("errorMessage", errorMessage ?? "");

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Save command log failed but ignored. CommandId: {CommandId}, Status: {Status}",
                    command.CommandId,
                    status);
            }
        }
    }
}