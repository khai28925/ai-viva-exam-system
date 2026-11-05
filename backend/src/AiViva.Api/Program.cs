using AiViva.Api.Extensions;
using AiViva.Api.Modules;
using AiViva.Application;
using AiViva.Infrastructure;
using AiViva.Infrastructure.Accounts;
using AiViva.Infrastructure.Configuration;

LocalEnvironment.Load();
var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi();
builder.Services.AddApiAuth(builder.Environment, builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}
app.UseRouting();
app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseApiAntiforgery();

await AdminBootstrapper.SeedAsync(app.Services, app.Configuration);

var api = app.MapGroup("/api");
api.MapApiModules();

app.Run();

public partial class Program;
