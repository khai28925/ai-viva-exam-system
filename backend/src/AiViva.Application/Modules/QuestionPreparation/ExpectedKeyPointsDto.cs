using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiViva.Application.ExpectedKeyPoints;

public sealed record ExpectedKeyPointsDto(
    Guid Id,
    Guid QuestionId,
    string Content,
    int DisplayOrder,
    DateTimeOffset CreatedAt);

public sealed record CreateExpectedKeyPointsRequest
{
    [JsonConstructor]
    public CreateExpectedKeyPointsRequest(string? content)
    {
        Content = content?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Content is required.")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Content must contain 1 to 2000 characters.")]
    public string Content { get; }
}

public sealed record UpdateExpectedKeyPointsRequest
{
    [JsonConstructor]
    public UpdateExpectedKeyPointsRequest(string? content)
    {
        Content = content?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Content is required.")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Content must contain 1 to 2000 characters.")]
    public string Content { get; }
}

public enum ExpectedKeyPointsError
{
    None,
    Invalid,
    QuestionNotFound,
    KeyPointNotFound,
    PreparationLocked,
    ConcurrentWrite
}

public readonly record struct ExpectedKeyPointsResult<T>(
    T? Value,
    ExpectedKeyPointsError Error = ExpectedKeyPointsError.None,
    string? Detail = null,
    IDictionary<string, string[]>? ValidationErrors = null)
{
    public bool IsSuccess => Error == ExpectedKeyPointsError.None;

    public static ExpectedKeyPointsResult<T> Success(T value) => new(value);

    public static ExpectedKeyPointsResult<T> Fail(
        ExpectedKeyPointsError error,
        string detail,
        IDictionary<string, string[]>? validationErrors = null) =>
        new(default, error, detail, validationErrors);
}

public static class ExpectedKeyPointsValidation
{
    public static IDictionary<string, string[]>? Validate(object request)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);
        return errors.Count == 0
            ? null
            : new Dictionary<string, string[]>
            {
                ["content"] = errors.Select(error => error.ErrorMessage ?? "Invalid content.").Distinct().ToArray()
            };
    }
}

