using Mersad.Api.Data;
using Microsoft.EntityFrameworkCore;
using Mersad.Api.Detection;
using Mersad.Api.Detection.Rules;
using Mersad.Api.Alerts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MersadDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MersadDb")));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddScoped<IDetectionRule, SuspiciousPowerShellRule>();
builder.Services.AddScoped<IDetectionRule, SuspiciousNetworkPortRule>();
builder.Services.AddScoped<IDetectionRule, PowerShellSuspiciousNetworkRule>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<DetectionEngine>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
