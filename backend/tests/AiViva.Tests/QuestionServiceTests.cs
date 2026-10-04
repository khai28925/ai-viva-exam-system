using System;
using System.Threading.Tasks;
using AiViva.Application.Questions;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace AiViva.Tests;

public sealed class QuestionServiceTests
{
    private static (InMemoryQuestionRepository, InMemoryQuestionBankRepository, QuestionService) CreateService()
    {
        var questionRepo = new InMemoryQuestionRepository();
        var bankRepo = new InMemoryQuestionBankRepository();
        var service = new QuestionService(questionRepo, bankRepo);
        return (questionRepo, bankRepo, service);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSeedData()
    {
        var (_, _, service) = CreateService();
        var result = await service.GetAllAsync();
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsQuestion()
    {
        var (_, _, service) = CreateService();
        var id = Guid.Parse("b1b2c3d4-0001-0000-0000-000000000001");
        
        var result = await service.GetByIdAsync(id);
        
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Type.Should().Be(QuestionType.Essay);
        result.DifficultyLevel.Should().Be(DifficultyLevel.Easy);
        result.TypeName.Should().Be(QuestionType.Essay.ToString());
        result.DifficultyLevelName.Should().Be(DifficultyLevel.Easy.ToString());
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var (_, _, service) = CreateService();
        var result = await service.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByQuestionBankIdAsync_ReturnsFilteredQuestions()
    {
        var (_, _, service) = CreateService();
        var bankId = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        
        var result = await service.GetByQuestionBankIdAsync(bankId);
        
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(q => q.QuestionBankId.Should().Be(bankId));
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsNewQuestion()
    {
        var (_, _, service) = CreateService();
        var bankId = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var request = new CreateQuestionRequest(bankId, "New Question", QuestionType.MultipleChoice, DifficultyLevel.Medium, "Explanation", 5);
        
        var result = await service.CreateAsync(request);
        
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.QuestionBankId.Should().Be(bankId);
        result.Content.Should().Be("New Question");
        result.Type.Should().Be(QuestionType.MultipleChoice);
        result.DifficultyLevel.Should().Be(DifficultyLevel.Medium);
        result.Points.Should().Be(5);
        
        var allQuestions = await service.GetAllAsync();
        allQuestions.Should().Contain(q => q.Id == result.Id);
    }

    [Fact]
    public async Task CreateAsync_EmptyContent_ThrowsArgumentException()
    {
        var (_, _, service) = CreateService();
        var bankId = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var request = new CreateQuestionRequest(bankId, "", QuestionType.MultipleChoice, DifficultyLevel.Medium, null, 1);
        
        var action = async () => await service.CreateAsync(request);
        
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_InvalidQuestionBankId_ThrowsArgumentException()
    {
        var (_, _, service) = CreateService();
        var bankId = Guid.NewGuid();
        var request = new CreateQuestionRequest(bankId, "Valid Content", QuestionType.MultipleChoice, DifficultyLevel.Medium, null, 1);
        
        var action = async () => await service.CreateAsync(request);
        
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_ExistingId_UpdatesFields()
    {
        var (_, _, service) = CreateService();
        var id = Guid.Parse("b1b2c3d4-0001-0000-0000-000000000001");
        var request = new UpdateQuestionRequest("Updated Content", QuestionType.TrueFalse, DifficultyLevel.Hard, "New Explanation", 10);
        
        var result = await service.UpdateAsync(id, request);
        
        result.Should().NotBeNull();
        result!.Content.Should().Be("Updated Content");
        result.Type.Should().Be(QuestionType.TrueFalse);
        result.DifficultyLevel.Should().Be(DifficultyLevel.Hard);
        result.Explanation.Should().Be("New Explanation");
        result.Points.Should().Be(10);
        result.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ReturnsNull()
    {
        var (_, _, service) = CreateService();
        var request = new UpdateQuestionRequest("Content", null, null, null, null);
        
        var result = await service.UpdateAsync(Guid.NewGuid(), request);
        
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_ReturnsTrue()
    {
        var (_, _, service) = CreateService();
        var id = Guid.Parse("b1b2c3d4-0001-0000-0000-000000000001");
        
        var result = await service.DeleteAsync(id);
        
        result.Should().BeTrue();
        
        var getResult = await service.GetByIdAsync(id);
        getResult.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        var (_, _, service) = CreateService();
        var result = await service.DeleteAsync(Guid.NewGuid());
        result.Should().BeFalse();
    }
}
