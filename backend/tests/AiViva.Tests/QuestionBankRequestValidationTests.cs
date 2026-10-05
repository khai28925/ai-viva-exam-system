using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AiViva.Application.QuestionBanks;
using AiViva.Application.Questions;
using Xunit;

namespace AiViva.Tests;

public sealed class QuestionBankRequestValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BankRequestsTrimBeforeLengthValidation(bool update)
    {
        var name = new string('n', 120);
        var description = new string('d', 500);
        var json = JsonSerializer.Serialize(new { name = $"  {name}  ", description = $"  {description}  " });
        object request = update
            ? JsonSerializer.Deserialize<UpdateQuestionBankRequest>(json, JsonSerializerOptions.Web)!
            : JsonSerializer.Deserialize<CreateQuestionBankRequest>(json, JsonSerializerOptions.Web)!;

        Assert.True(IsValid(request));
        Assert.Equal(name, update ? ((UpdateQuestionBankRequest)request).Name : ((CreateQuestionBankRequest)request).Name);
        Assert.Equal(description, update ? ((UpdateQuestionBankRequest)request).Description : ((CreateQuestionBankRequest)request).Description);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QuestionRequestsTrimBeforeLengthValidation(bool update)
    {
        var content = new string('q', 2000);
        var json = JsonSerializer.Serialize(new { content = $"  {content}  " });
        object request = update
            ? JsonSerializer.Deserialize<UpdateQuestionRequest>(json, JsonSerializerOptions.Web)!
            : JsonSerializer.Deserialize<CreateQuestionRequest>(json, JsonSerializerOptions.Web)!;

        Assert.True(IsValid(request));
        Assert.Equal(content, update ? ((UpdateQuestionRequest)request).Content : ((CreateQuestionRequest)request).Content);
    }

    [Fact]
    public void InvalidOrMissingRequiredFieldsAreRejected()
    {
        Assert.False(IsValid(new CreateQuestionBankRequest("   ")));
        Assert.False(IsValid(new UpdateQuestionBankRequest(new string('n', 121))));
        Assert.False(IsValid(new CreateQuestionBankRequest("Bank", new string('d', 501))));
        Assert.False(IsValid(new CreateQuestionRequest("   ")));
        Assert.False(IsValid(new UpdateQuestionRequest(new string('q', 2001))));
        Assert.False(IsValid(JsonSerializer.Deserialize<CreateQuestionBankRequest>("{}", JsonSerializerOptions.Web)!));
        Assert.False(IsValid(JsonSerializer.Deserialize<CreateQuestionRequest>("{}", JsonSerializerOptions.Web)!));
        Assert.Null(new CreateQuestionBankRequest("Bank", "   ").Description);
        Assert.Null(new UpdateQuestionBankRequest("Bank", "   ").Description);
    }

    private static bool IsValid(object request)
    {
        return Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true);
    }
}
