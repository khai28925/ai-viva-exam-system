using AiViva.Application.Accounts;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AiViva.Infrastructure.Accounts;

public sealed class AccountService(
    QuestionBankDbContext db,
    IPasswordHasher<UserAccount> passwordHasher) : IAccountService
{
    public async Task<AccountDto?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        var normalizedEmail = UserAccount.NormalizeEmail(email);
        var user = await db.Users
            .AsNoTracking()
            .Include(account => account.Role)
            .SingleOrDefaultAsync(account => account.NormalizedEmail == normalizedEmail, ct);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : ToDto(user);
    }

    public async Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(account => account.Role)
            .SingleOrDefaultAsync(account => account.Id == id && account.IsActive, ct);
        return user is null ? null : ToDto(user);
    }

    public async Task<IReadOnlyCollection<AccountDto>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await db.Users
            .AsNoTracking()
            .Include(account => account.Role)
            .OrderBy(account => account.Email)
            .ToArrayAsync(ct);
        return users.Select(ToDto).ToArray();
    }

    public async Task<CreateAccountResult> CreateAsync(
        CreateAccountRequest request,
        CancellationToken ct = default)
    {
        var roleCode = request.Role.Trim().ToUpperInvariant();
        if (roleCode is not (Role.Codes.Student or Role.Codes.Lecturer))
        {
            return new CreateAccountResult(CreateAccountStatus.InvalidRole);
        }

        var normalizedEmail = UserAccount.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(account => account.NormalizedEmail == normalizedEmail, ct))
        {
            return new CreateAccountResult(CreateAccountStatus.DuplicateEmail);
        }

        var role = await db.Roles.SingleAsync(item => item.Code == roleCode, ct);
        var user = new UserAccount(request.Email, role.Id);
        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
            return new CreateAccountResult(
                CreateAccountStatus.Created,
                new AccountDto(user.Id, user.Email, role.Code));
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new CreateAccountResult(CreateAccountStatus.DuplicateEmail);
        }
    }

    private static AccountDto ToDto(UserAccount user) =>
        new(user.Id, user.Email, user.Role.Code);
}
