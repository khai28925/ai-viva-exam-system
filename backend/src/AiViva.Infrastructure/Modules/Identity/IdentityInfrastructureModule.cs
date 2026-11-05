using AiViva.Application.Accounts;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Infrastructure.Modules.Identity;

public static class IdentityInfrastructureModule
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        return services;
    }
}
