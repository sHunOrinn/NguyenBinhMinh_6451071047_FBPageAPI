using BackendAPI.Models;
using Npgsql;

namespace BackendAPI.Services
{
    public class CommandLogService
    {
        private readonly IConfiguration _configuration;

        public CommandLogService(IConfiguration configuration)
        {
            _configuration = configuration;
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
            cmd.Parameters.AddWithValue("commandId", command.CommandId);
            cmd.Parameters.AddWithValue("eventId", command.EventId);
            cmd.Parameters.AddWithValue("commentId", command.CommentId ?? "");
            cmd.Parameters.AddWithValue("action", command.Action);
            cmd.Parameters.AddWithValue("replyText", command.ReplyText ?? "");
            cmd.Parameters.AddWithValue("intent", command.Intent);
            cmd.Parameters.AddWithValue("sentiment", command.Sentiment);
            cmd.Parameters.AddWithValue("status", status);
            cmd.Parameters.AddWithValue("retryCount", command.RetryCount);
            cmd.Parameters.AddWithValue("errorMessage", errorMessage ?? "");

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}