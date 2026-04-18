using NguyenBinhMinh_FBPageAPI.Models;

var builder = WebApplication.CreateBuilder(args);

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