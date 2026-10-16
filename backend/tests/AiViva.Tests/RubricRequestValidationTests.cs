using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AiViva.Application.Rubrics;
using Xunit;

namespace AiViva.Tests;

public sealed class RubricRequestValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RubricRequestsTrimBeforeValidation(bool update)
    {
        var name = new string('r', 120);
        var description = new string('d', 500);
        var json = JsonSerializer.Serialize(new { name = $"  {name}  ", description = $"  {description}  " });
        object request = update
            ? JsonSerializer.Deserialize<UpdateRubricRequest>(json, JsonSerializerOptions.Web)!
            : JsonSerializer.Deserialize<CreateRubricRequest>(json, JsonSerializerOptions.Web)!;

        Assert.True(IsValid(request));
        Assert.Equal(name, update ? ((UpdateRubricRequest)request).Name : ((CreateRubricRequest)request).Name);
        Assert.Equal(description, update ? ((UpdateRubricRequest)request).Description : ((CreateRubricRequest)request).Description);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CriterionRequestsTrimBeforeValidation(bool update)
    {
        var name = new string('c', 120);
        var description = new string('d', 500);
        var json = JsonSerializer.Serialize(new
        {
            name = $"  {name}  ",
            description = $"  {description}  ",
            maxScore = 10.00m,
            weight = 0.5000m
        });

        object request = update
            ? JsonSerializer.Deserialize<UpdateRubricCriterionRequest>(json, JsonSerializerOptions.Web)!
            : JsonSerializer.Deserialize<CreateRubricCriterionRequest>(json, JsonSerializerOptions.Web)!;

        Assert.True(IsValid(request));
        Assert.Equal(name, update ? ((UpdateRubricCriterionRequest)request).Name : ((CreateRubricCriterionRequest)request).Name);
        Assert.Equal(description, update ? ((UpdateRubricCriterionRequest)request).Description : ((CreateRubricCriterionRequest)request).Description);
    }

    [Fact]
    public void MaxScoreMustBeGreaterThanZeroAndAtMostTwoDecimals()
    {
        // Valid max scores
        Assert.True(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 10.00m, 0.5m)));
        Assert.True(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 0.01m, 0.5m)));
        Assert.True(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 100m, 0.5m)));

        // Invalid: <= 0
        Assert.False(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 0m, 0.5m)));
        Assert.False(IsValid(new CreateRubricCriterionRequest("Accuracy", null, -5.00m, 0.5m)));

        // Invalid: > 2 decimal places
        Assert.False(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 10.125m, 0.5m)));
        Assert.False(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 0.001m, 0.5m)));
    }

    [Fact]
    public void WeightMustBeGreaterThanZeroUpToOneAndAtMostFourDecimals()
    {
        // Valid weights
        Assert.True(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 10m, 0.0001m)));
        Assert.True(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 10m, 0.5000m)));
        Assert.True(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 10m, 1.0000m)));

        // Invalid: <= 0 or > 1
        Assert.False(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 10m, 0m)));
        Assert.False(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 10m, -0.1m)));
        Assert.False(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 10m, 1.0001m)));
        Assert.False(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 10m, 2.0m)));

        // Invalid: > 4 decimal places
        Assert.False(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 10m, 0.12345m)));
        Assert.False(IsValid(new CreateRubricCriterionRequest("Accuracy", null, 10m, 0.00001m)));
    }

    [Fact]
    public void InvalidOrMissingRequiredFieldsAreRejected()
    {
        // Empty names
        Assert.False(IsValid(new CreateRubricRequest("   ")));
        Assert.False(IsValid(new UpdateRubricRequest("   ")));
        Assert.False(IsValid(new CreateRubricCriterionRequest("   ", null, 10m, 0.5m)));
        Assert.False(IsValid(new UpdateRubricCriterionRequest("   ", null, 10m, 0.5m)));

        // Name exceeding 120
        Assert.False(IsValid(new CreateRubricRequest(new string('a', 121))));
        Assert.False(IsValid(new UpdateRubricRequest(new string('a', 121))));
        Assert.False(IsValid(new CreateRubricCriterionRequest(new string('a', 121), null, 10m, 0.5m)));
        Assert.False(IsValid(new UpdateRubricCriterionRequest(new string('a', 121), null, 10m, 0.5m)));

        // Description exceeding 500
        Assert.False(IsValid(new CreateRubricRequest("Rubric", new string('d', 501))));
        Assert.False(IsValid(new UpdateRubricRequest("Rubric", new string('d', 501))));
        Assert.False(IsValid(new CreateRubricCriterionRequest("Criterion", new string('d', 501), 10m, 0.5m)));
        Assert.False(IsValid(new UpdateRubricCriterionRequest("Criterion", new string('d', 501), 10m, 0.5m)));

        // Whitespace description becomes null
        Assert.Null(new CreateRubricRequest("Rubric", "   ").Description);
        Assert.Null(new UpdateRubricRequest("Rubric", "   ").Description);
        Assert.Null(new CreateRubricCriterionRequest("Criterion", "   ", 10m, 0.5m).Description);
        Assert.Null(new UpdateRubricCriterionRequest("Criterion", "   ", 10m, 0.5m).Description);

        // Deserializing empty object fails required name
        Assert.False(IsValid(JsonSerializer.Deserialize<CreateRubricRequest>("{}", JsonSerializerOptions.Web)!));
        Assert.False(IsValid(JsonSerializer.Deserialize<CreateRubricCriterionRequest>("{}", JsonSerializerOptions.Web)!));
    }

    [Fact]
    public void RubricValidationHelperReturnsCamelCaseFieldErrors()
    {
        var invalidCriterion = new CreateRubricCriterionRequest("", new string('d', 501), 0m, 2m);
        var errors = RubricValidation.Validate(invalidCriterion);

        Assert.NotNull(errors);
        Assert.Contains("name", errors.Keys);
        Assert.Contains("description", errors.Keys);
        Assert.Contains("maxScore", errors.Keys);
        Assert.Contains("weight", errors.Keys);
    }

    private static bool IsValid(object request)
    {
        return Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true);
    }
}
