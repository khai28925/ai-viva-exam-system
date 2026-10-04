using System;
using System.Threading.Tasks;
using AiViva.Application.QuestionBanks;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace AiViva.Tests;

public sealed class QuestionBankServiceTests
{
    private static (InMemoryQuestionBankRepository, InMemoryQuestionRepository, QuestionBankService) CreateService()
    {
        var bankRepo = new InMemoryQuestionBankRepository();
        var questionRepo = new InMemoryQuestionRepository();
        var service = new QuestionBankService(bankRepo, questionRepo);
        return (bankRepo, questionRepo, service);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSeedData()
    {
        var (_, _, service) = CreateService();
        var result = await service.GetAllAsync();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsBank()
    {
        var (_, _, service) = CreateService();
        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        
        var result = await service.GetByIdAsync(id);
        
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Title.Should().Be("Software Engineering Basics");
        result.QuestionCount.Should().Be(2); // Since seed data has 2 questions for bank1
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var (_, _, service) = CreateService();
        var result = await service.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsNewBank()
    {
        var (_, _, service) = CreateService();
        var subjectId = Guid.NewGuid();
        var request = new CreateQuestionBankRequest("New Bank", "Description", subjectId);
        
        var result = await service.CreateAsync(request);
        
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Title.Should().Be("New Bank");
        result.Description.Should().Be("Description");
        result.SubjectId.Should().Be(subjectId);
        
        var allBanks = await service.GetAllAsync();
        allBanks.Should().Contain(b => b.Id == result.Id);
    }

    [Fact]
    public async Task CreateAsync_EmptyTitle_ThrowsArgumentException()
    {
        var (_, _, service) = CreateService();
        var request = new CreateQuestionBankRequest("", "Description", Guid.NewGuid());
        
        var action = async () => await service.CreateAsync(request);
        
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_ExistingId_UpdatesFields()
    {
        var (_, _, service) = CreateService();
        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        var request = new UpdateQuestionBankRequest("Updated Title", "Updated Description");
        
        var result = await service.UpdateAsync(id, request);
        
        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated Title");
        result.Description.Should().Be("Updated Description");
        result.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ReturnsNull()
    {
        var (_, _, service) = CreateService();
        var request = new UpdateQuestionBankRequest("Updated Title", null);
        
        var result = await service.UpdateAsync(Guid.NewGuid(), request);
        
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_ReturnsTrue()
    {
        var (_, _, service) = CreateService();
        var id = Guid.Parse("a1b2c3d4-0001-0000-0000-000000000001");
        
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
