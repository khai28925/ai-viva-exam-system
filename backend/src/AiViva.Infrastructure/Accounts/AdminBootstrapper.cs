using System.ComponentModel.DataAnnotations;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiViva.Infrastructure.Accounts;

public static class AdminBootstrapper
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        var email = configuration["AIVES_BOOTSTRAP_ADMIN_EMAIL"];
        var password = configuration["AIVES_BOOTSTRAP_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(email) ||
            !new EmailAddressAttribute().IsValid(email) ||
            string.IsNullOrWhiteSpace(password) ||
            password.Length is < 12 or > 128)
        {
            throw new InvalidOperationException(
                "Bootstrap admin requires a valid AIVES_BOOTSTRAP_ADMIN_EMAIL and AIVES_BOOTSTRAP_ADMIN_PASSWORD (12-128 characters).");
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionBankDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserAccount>>();
        var adminRole = await db.Roles.SingleAsync(role => role.Code == Role.Codes.Admin, ct);
        var normalizedEmail = UserAccount.NormalizeEmail(email);
        var existing = await db.Users
            .Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, ct);
        if (existing is not null)
        {
            if (existing.Role.Code != Role.Codes.Admin)
            {
                throw new InvalidOperationException(
                    "Bootstrap email already belongs to a non-admin account.");
            }

            return;
        }

        if (await db.Users.AnyAsync(user => user.RoleId == adminRole.Id, ct))
        {
            return;
        }

        var admin = new UserAccount(email, adminRole.Id);
        admin.SetPasswordHash(hasher.HashPassword(admin, password));
        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);
    }
}
