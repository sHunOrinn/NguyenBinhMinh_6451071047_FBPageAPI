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
        Environment.GetEnvironmentVariable("FB_APP_SECRET") ?? "";
});

// Services
builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddSingleton<FacebookSignatureService>();
builder.Services.AddSingleton<FacebookEventNormalizer>();

builder.Services.Configure<FacebookOptions>(
    builder.Configuration.GetSection("Facebook"));

builder.Services.AddHttpClient();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();