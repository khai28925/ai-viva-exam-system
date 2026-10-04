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
    private static async Task<(InMemoryQuestionBankRepository, QuestionService, Guid)> CreateServiceWithBankAsync()
    {
        var repo = new InMemoryQuestionBankRepository();
        var bank = new QuestionBank("Sample Bank", "Description");
        await repo.AddAsync(bank);
        var service = new QuestionService(repo);
        return (repo, service, bank.Id);
    }

    [Fact]
    public async Task GetByQuestionBankIdAsync_NonExistentBank_ReturnsNull()
    {
        var (repo, service, _) = await CreateServiceWithBankAsync();
        var result = await service.GetByQuestionBankIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByQuestionBankIdAsync_EmptyBank_ReturnsEmptyList()
    {
        var (_, service, bankId) = await CreateServiceWithBankAsync();
        var result = await service.GetByQuestionBankIdAsync(bankId);
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsNewQuestion()
    {
        var (_, service, bankId) = await CreateServiceWithBankAsync();
        var request = new CreateQuestionRequest("What is polymorphism?");

        var result = await service.CreateAsync(bankId, request);

        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
        result.QuestionBankId.Should().Be(bankId);
        result.Content.Should().Be("What is polymorphism?");

        var list = await service.GetByQuestionBankIdAsync(bankId);
        list.Should().ContainSingle(q => q.Id == result.Id);
    }

    [Fact]
    public async Task CreateAsync_NonExistentBank_ReturnsNull()
    {
        var (_, service, _) = await CreateServiceWithBankAsync();
        var request = new CreateQuestionRequest("Sample content");

        var result = await service.CreateAsync(Guid.NewGuid(), request);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_EmptyContent_ThrowsArgumentException()
    {
        var (_, service, bankId) = await CreateServiceWithBankAsync();
        var request = new CreateQuestionRequest("");

        var action = async () => await service.CreateAsync(bankId, request);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsQuestion()
    {
        var (_, service, bankId) = await CreateServiceWithBankAsync();
        var created = await service.CreateAsync(bankId, new CreateQuestionRequest("Explain encapsulation."));

        var result = await service.GetByIdAsync(bankId, created!.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.Content.Should().Be("Explain encapsulation.");
        result.QuestionBankId.Should().Be(bankId);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var (_, service, bankId) = await CreateServiceWithBankAsync();
        var result = await service.GetByIdAsync(bankId, Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ExistingId_UpdatesContent()
    {
        var (_, service, bankId) = await CreateServiceWithBankAsync();
        var created = await service.CreateAsync(bankId, new CreateQuestionRequest("Initial question"));
        var updateRequest = new UpdateQuestionRequest("Updated question text");

        var result = await service.UpdateAsync(bankId, created!.Id, updateRequest);

        result.Should().NotBeNull();
        result!.Content.Should().Be("Updated question text");

        var fetched = await service.GetByIdAsync(bankId, created.Id);
        fetched!.Content.Should().Be("Updated question text");
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ReturnsNull()
    {
        var (_, service, bankId) = await CreateServiceWithBankAsync();
        var updateRequest = new UpdateQuestionRequest("Updated question text");

        var result = await service.UpdateAsync(bankId, Guid.NewGuid(), updateRequest);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_ReturnsTrue()
    {
        var (_, service, bankId) = await CreateServiceWithBankAsync();
        var created = await service.CreateAsync(bankId, new CreateQuestionRequest("Question to delete"));

        var result = await service.DeleteAsync(bankId, created!.Id);

        result.Should().BeTrue();

        var getResult = await service.GetByIdAsync(bankId, created.Id);
        getResult.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        var (_, service, bankId) = await CreateServiceWithBankAsync();
        var result = await service.DeleteAsync(bankId, Guid.NewGuid());
        result.Should().BeFalse();
    }
}
