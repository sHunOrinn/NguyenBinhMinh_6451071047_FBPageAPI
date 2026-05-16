using NguyenBinhMinh_FBPageAPI.Models;
using NguyenBinhMinh_FBPageAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Kafka config
builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection("Kafka"));

// Facebook Webhook config
builder.Services.Configure<FacebookWebhookOptions>(options =>
{
    options.VerifyToken =
        builder.Configuration["FacebookWebhook:VerifyToken"] ?? "my_verify_token";

    options.AppSecret =
        Environment.GetEnvironmentVariable("FB_APP_SECRET")
        ?? builder.Configuration["FacebookWebhook:AppSecret"]
        ?? "";
});

// Facebook config
builder.Services.Configure<FacebookOptions>(
    builder.Configuration.GetSection("Facebook"));

// HttpClient
builder.Services.AddHttpClient();

// Core services
builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddSingleton<FacebookSignatureService>();
builder.Services.AddSingleton<FacebookEventNormalizer>();

builder.Services.AddSingleton<EventStateStore>();
builder.Services.AddSingleton<SpamDetectionService>();
builder.Services.AddSingleton<AiClassificationService>();
builder.Services.AddSingleton<EventDecisionService>();

builder.Services.AddScoped<CoreEventProcessorService>();

// Bài 2 chỉ consume raw_events và publish reply_commands
builder.Services.AddHostedService<RawEventsConsumerHostedService>();

// Tạm thời KHÔNG bật phần gọi Facebook API / Retry Service ở Bài 2
builder.Services.AddScoped<FacebookCommentActionService>();
builder.Services.AddHostedService<ReplyCommandsConsumerHostedService>();
builder.Services.AddHostedService<RetryFailedConsumerHostedService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Nếu dùng ngrok hoặc test local webhook, có thể tạm comment dòng này
// app.UseHttpsRedirection();

app.MapControllers();

app.Run();