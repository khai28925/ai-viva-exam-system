namespace AiViva.Application.Accounts;

public interface IAccountService
{
    Task<AccountDto?> AuthenticateAsync(string email, string password, CancellationToken ct = default);

    Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyCollection<AccountDto>> GetAllAsync(CancellationToken ct = default);

    Task<CreateAccountResult> CreateAsync(CreateAccountRequest request, CancellationToken ct = default);
}
