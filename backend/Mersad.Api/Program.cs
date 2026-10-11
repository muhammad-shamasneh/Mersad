using Mersad.Api.Data;
using Microsoft.EntityFrameworkCore;
using Mersad.Api.Detection;
using Mersad.Api.Detection.Rules;
using Mersad.Api.Alerts;
using Mersad.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<DetectionOptions>()
    .Bind(builder.Configuration.GetSection(
        DetectionOptions.SectionName))
    .Validate(
        options => options.IsValid(),
        "Detection configuration contains invalid or duplicate ports.")
    .ValidateOnStart();

builder.Services.AddDbContext<MersadDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MersadDb")));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddScoped<IDetectionRule, SuspiciousPowerShellRule>();
builder.Services.AddScoped<IDetectionRule, SuspiciousNetworkPortRule>();
builder.Services.AddScoped<IDetectionRule, PowerShellSuspiciousNetworkRule>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<DetectionEngine>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Dashboard", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("Dashboard");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
