using System.Security.Claims;
using AiViva.Api.Extensions;
using AiViva.Application.Accounts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AiViva.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/v1/auth").WithTags("Authentication");

        auth.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext context) =>
        {
            var token = antiforgery.GetAndStoreTokens(context).RequestToken;
            return Results.Ok(new { token });
        })
        .WithName("GetCsrfToken");

        auth.MapPost("/login", async (
            LoginRequest request,
            IAccountService accounts,
            HttpContext context,
            CancellationToken ct) =>
        {
            var account = await accounts.AuthenticateAsync(request.Email, request.Password, ct);
            if (account is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid email or password");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
                new Claim(ClaimTypes.Name, account.Email),
                new Claim(ClaimTypes.Role, account.Role)
            };
            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
            return Results.Ok(account);
        })
        .RequireRateLimiting(AuthPolicies.LoginRateLimit)
        .WithName("Login")
        .Produces<AccountDto>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = principal.FindFirstValue(ClaimTypes.Name);
            var role = principal.FindFirstValue(ClaimTypes.Role);
            return Guid.TryParse(id, out var userId) && email is not null && role is not null
                ? Results.Ok(new AccountDto(userId, email, role))
                : Results.Unauthorized();
        })
        .RequireAuthorization()
        .WithName("GetCurrentAccount")
        .Produces<AccountDto>()
        .Produces(StatusCodes.Status401Unauthorized);

        auth.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("Logout")
        .Produces(StatusCodes.Status204NoContent);

        return group;
    }
}
