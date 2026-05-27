using CoreServices.Models;
using CoreServices.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection("Kafka"));

builder.Services.AddSingleton<KafkaProducerService>();

builder.Services.AddSingleton<EventStateStore>();
builder.Services.AddSingleton<SpamDetectionService>();

builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection("Gemini"));

builder.Services.AddHttpClient();

builder.Services.AddSingleton<AiClassificationService>();
builder.Services.AddSingleton<EventDecisionService>();

builder.Services.AddScoped<CoreEventProcessorService>();

builder.Services.AddHostedService<RawEventsConsumerHostedService>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

//app.UseAuthorization();

app.MapControllers();

app.Run();
