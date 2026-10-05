using AiViva.Api.Extensions;
using AiViva.Application.Accounts;

namespace AiViva.Api.Endpoints;

public static class AdminUserEndpoints
{
    public static RouteGroupBuilder MapAdminUserEndpoints(this RouteGroupBuilder group)
    {
        var users = group.MapGroup("/v1/admin/users")
            .WithTags("Admin Users")
            .RequireAuthorization(AuthPolicies.AdminOnly);

        users.MapGet("", async (IAccountService accounts, CancellationToken ct) =>
            Results.Ok(await accounts.GetAllAsync(ct)))
            .WithName("GetUsers")
            .Produces<IReadOnlyCollection<AccountDto>>();

        users.MapGet("/{userId:guid}", async (
            Guid userId,
            IAccountService accounts,
            CancellationToken ct) =>
        {
            var account = await accounts.GetByIdAsync(userId, ct);
            return account is null ? Results.NotFound() : Results.Ok(account);
        })
        .WithName("GetUserById")
        .Produces<AccountDto>()
        .Produces(StatusCodes.Status404NotFound);

        users.MapPost("", async (
            CreateAccountRequest request,
            IAccountService accounts,
            CancellationToken ct) =>
        {
            var result = await accounts.CreateAsync(request, ct);
            return result.Status switch
            {
                CreateAccountStatus.Created => Results.Created(
                    $"/api/v1/admin/users/{result.Account!.Id}",
                    result.Account),
                CreateAccountStatus.DuplicateEmail => Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Email already exists"),
                _ => Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["role"] = ["Role must be STUDENT or LECTURER."]
                })
            };
        })
        .WithName("CreateUser")
        .Produces<AccountDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem();

        return group;
    }
}
