using System;
using System.Threading.Tasks;
using AiViva.Application.Abstractions;
using AiViva.Application.QuestionBanks;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace AiViva.Tests;

public sealed class QuestionBankServiceTests
{
    private static (InMemoryQuestionBankRepository, QuestionBankService) CreateService()
    {
        var repo = new InMemoryQuestionBankRepository();
        var service = new QuestionBankService(repo);
        return (repo, service);
    }

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        var (_, service) = CreateService();
        var result = await service.GetAllAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsNewBank()
    {
        var (_, service) = CreateService();
        var request = new CreateQuestionBankRequest("Software Engineering Basics", "Fundamental SE concepts");

        var result = await service.CreateAsync(request);

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Software Engineering Basics");
        result.Description.Should().Be("Fundamental SE concepts");

        var allBanks = await service.GetAllAsync();
        allBanks.Should().ContainSingle(b => b.Id == result.Id);
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ThrowsArgumentException()
    {
        var (_, service) = CreateService();
        var request = new CreateQuestionBankRequest("", "Description");

        var action = async () => await service.CreateAsync(request);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsBank()
    {
        var (_, service) = CreateService();
        var created = await service.CreateAsync(new CreateQuestionBankRequest("Algorithms", "Algo concepts"));

        var result = await service.GetByIdAsync(created.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.Name.Should().Be("Algorithms");
        result.Description.Should().Be("Algo concepts");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var (_, service) = CreateService();
        var result = await service.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ExistingId_UpdatesDetails()
    {
        var (_, service) = CreateService();
        var created = await service.CreateAsync(new CreateQuestionBankRequest("Original Name", "Original Desc"));
        var updateRequest = new UpdateQuestionBankRequest("Updated Name", "Updated Desc");

        var result = await service.UpdateAsync(created.Id, updateRequest);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Desc");

        var fetched = await service.GetByIdAsync(created.Id);
        fetched!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ReturnsNull()
    {
        var (_, service) = CreateService();
        var updateRequest = new UpdateQuestionBankRequest("Updated Name", null);

        var result = await service.UpdateAsync(Guid.NewGuid(), updateRequest);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ExistingIdWithoutQuestions_ReturnsDeleted()
    {
        var (_, service) = CreateService();
        var created = await service.CreateAsync(new CreateQuestionBankRequest("Bank to delete", null));

        var result = await service.DeleteAsync(created.Id);

        result.Should().Be(QuestionBankDeleteResult.Deleted);

        var getResult = await service.GetByIdAsync(created.Id);
        getResult.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsNotFound()
    {
        var (_, service) = CreateService();
        var result = await service.DeleteAsync(Guid.NewGuid());
        result.Should().Be(QuestionBankDeleteResult.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_WithQuestions_ReturnsHasQuestions()
    {
        var (repo, service) = CreateService();
        var created = await service.CreateAsync(new CreateQuestionBankRequest("Bank with questions", null));
        var question = new Question(created.Id, "Sample question content");
        await repo.AddQuestionAsync(question);

        var result = await service.DeleteAsync(created.Id);

        result.Should().Be(QuestionBankDeleteResult.HasQuestions);

        var getResult = await service.GetByIdAsync(created.Id);
        getResult.Should().NotBeNull();
    }
}
