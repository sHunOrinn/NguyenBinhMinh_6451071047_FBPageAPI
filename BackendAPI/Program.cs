using BackendAPI.Models;
using BackendAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection("Kafka"));

builder.Services.Configure<FacebookOptions>(
    builder.Configuration.GetSection("Facebook"));

builder.Services.AddHttpClient();

builder.Services.AddSingleton<KafkaProducerService>();
//builder.Services.AddSingleton<InMemoryIdempotencyService>();
builder.Services.AddSingleton<SupabaseIdempotencyService>();
builder.Services.AddSingleton<CommandLogService>();

builder.Services.AddScoped<FacebookCommentActionService>();

builder.Services.AddHostedService<ReplyCommandsConsumerHostedService>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior =
        BackgroundServiceExceptionBehavior.Ignore;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseSwagger();
app.UseSwaggerUI();

//app.UseHttpsRedirection();

//app.UseAuthorization();

app.MapControllers();

app.Run();
