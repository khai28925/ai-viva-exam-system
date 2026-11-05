using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiViva.Application.Rubrics;

public sealed record RubricDto(
    Guid Id,
    Guid QuestionBankId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt);

public sealed record RubricCriterionDto(
    Guid Id,
    Guid RubricId,
    string Name,
    string? Description,
    decimal MaxScore,
    decimal Weight,
    DateTimeOffset CreatedAt);

public sealed record CreateRubricRequest
{
    public CreateRubricRequest(string name, string? description = null)
    {
        Name = name?.Trim() ?? string.Empty;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "Name must contain 1 to 120 characters.")]
    public string Name { get; init; }

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    public string? Description { get; init; }
}

public sealed record UpdateRubricRequest
{
    public UpdateRubricRequest(string name, string? description = null)
    {
        Name = name?.Trim() ?? string.Empty;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "Name must contain 1 to 120 characters.")]
    public string Name { get; init; }

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    public string? Description { get; init; }
}

public sealed record CreateRubricCriterionRequest : IValidatableObject
{
    [JsonConstructor]
    public CreateRubricCriterionRequest(
        string name,
        string? description = null,
        decimal maxScore = 0,
        decimal weight = 0)
    {
        Name = name?.Trim() ?? string.Empty;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        MaxScore = maxScore;
        Weight = weight;
    }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "Name must contain 1 to 120 characters.")]
    public string Name { get; init; }

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    public string? Description { get; init; }

    public decimal MaxScore { get; init; }

    public decimal Weight { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxScore <= 0)
        {
            yield return new ValidationResult("Max score must be greater than 0.", [nameof(MaxScore)]);
        }
        else if (decimal.Round(MaxScore, 2) != MaxScore)
        {
            yield return new ValidationResult("Max score must have at most 2 decimal places.", [nameof(MaxScore)]);
        }

        if (Weight <= 0 || Weight > 1)
        {
            yield return new ValidationResult("Weight must be greater than 0 and at most 1.", [nameof(Weight)]);
        }
        else if (decimal.Round(Weight, 4) != Weight)
        {
            yield return new ValidationResult("Weight must have at most 4 decimal places.", [nameof(Weight)]);
        }
    }
}

public sealed record UpdateRubricCriterionRequest : IValidatableObject
{
    [JsonConstructor]
    public UpdateRubricCriterionRequest(
        string name,
        string? description = null,
        decimal maxScore = 0,
        decimal weight = 0)
    {
        Name = name?.Trim() ?? string.Empty;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        MaxScore = maxScore;
        Weight = weight;
    }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "Name must contain 1 to 120 characters.")]
    public string Name { get; init; }

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters.")]
    public string? Description { get; init; }

    public decimal MaxScore { get; init; }

    public decimal Weight { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxScore <= 0)
        {
            yield return new ValidationResult("Max score must be greater than 0.", [nameof(MaxScore)]);
        }
        else if (decimal.Round(MaxScore, 2) != MaxScore)
        {
            yield return new ValidationResult("Max score must have at most 2 decimal places.", [nameof(MaxScore)]);
        }

        if (Weight <= 0 || Weight > 1)
        {
            yield return new ValidationResult("Weight must be greater than 0 and at most 1.", [nameof(Weight)]);
        }
        else if (decimal.Round(Weight, 4) != Weight)
        {
            yield return new ValidationResult("Weight must have at most 4 decimal places.", [nameof(Weight)]);
        }
    }
}

public enum RubricServiceError
{
    None,
    Invalid,
    BankNotFound,
    RubricNotFound,
    CriterionNotFound,
    RubricAlreadyExists,
    RubricHasCriteria,
    CriterionNameExists,
    PreparationLocked
}

public readonly record struct RubricServiceResult<T>(
    T? Value,
    RubricServiceError Error = RubricServiceError.None,
    string? Detail = null,
    IDictionary<string, string[]>? ValidationErrors = null)
{
    public bool IsSuccess => Error == RubricServiceError.None;

    public static RubricServiceResult<T> Success(T value) => new(value);

    public static RubricServiceResult<T> Fail(
        RubricServiceError error,
        string detail,
        IDictionary<string, string[]>? validationErrors = null) =>
        new(default, error, detail, validationErrors);
}

public static class RubricValidation
{
    public static IDictionary<string, string[]>? Validate(object request)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(request);
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);

        if (request is IValidatableObject validatable)
        {
            foreach (var vr in validatable.Validate(context))
            {
                results.Add(vr);
            }
        }

        if (results.Count == 0)
        {
            return null;
        }

        return results
            .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [""])
                .Select(m => new { Member = ToCamelCase(m), Error = r.ErrorMessage ?? "Invalid value." }))
            .GroupBy(x => x.Member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Error).Distinct().ToArray());
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
