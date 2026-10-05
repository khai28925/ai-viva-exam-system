using System.Security.Claims;
using System.Threading.RateLimiting;
using AiViva.Application.Accounts;
using AiViva.Domain.Entities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

namespace AiViva.Api.Extensions;

public static class AuthPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string QuestionBankManage = "QuestionBankManage";
    public const string LoginRateLimit = "LoginRateLimit";
}

public static class AuthExtensions
{
    public static IServiceCollection AddApiAuth(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "aives.auth";
                options.Cookie.Path = "/api";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.Cookie.IsEssential = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
                options.Events.OnValidatePrincipal = async context =>
                {
                    var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var role = context.Principal?.FindFirstValue(ClaimTypes.Role);
                    if (!Guid.TryParse(id, out var userId) || role is null)
                    {
                        context.RejectPrincipal();
                        return;
                    }

                    var accounts = context.HttpContext.RequestServices.GetRequiredService<IAccountService>();
                    var account = await accounts.GetByIdAsync(userId, context.HttpContext.RequestAborted);
                    if (account is null || account.Role != role)
                    {
                        context.RejectPrincipal();
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole(Role.Codes.Admin))
            .AddPolicy(
                AuthPolicies.QuestionBankManage,
                policy => policy.RequireRole(Role.Codes.Admin, Role.Codes.Lecturer));

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "aives.csrf";
            options.Cookie.Path = "/api";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthPolicies.LoginRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    public static IApplicationBuilder UseApiAntiforgery(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.Headers.CacheControl = "no-store";
            }

            if (context.Request.Path.StartsWithSegments("/api") &&
                !HttpMethods.IsGet(context.Request.Method) &&
                !HttpMethods.IsHead(context.Request.Method) &&
                !HttpMethods.IsOptions(context.Request.Method))
            {
                var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                if (!await antiforgery.IsRequestValidAsync(context))
                {
                    await Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid CSRF token")
                        .ExecuteAsync(context);
                    return;
                }
            }

            await next(context);
        });
    }
}
