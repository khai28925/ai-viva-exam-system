using AiViva.Application.Rubrics;
using AiViva.Domain.Entities;
using AiViva.Infrastructure.Persistence;
using AiViva.Infrastructure.Rubrics;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AiViva.Tests;

public sealed class RubricServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly QuestionBankDbContext _db;
    private readonly PostgresRubricService _service;
    private readonly Guid _bankId;
    private readonly Guid _lecturerId;

    public RubricServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<QuestionBankDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new QuestionBankDbContext(options);
        _db.Database.EnsureCreated();

        var role = _db.Roles.Single(r => r.Code == Role.Codes.Lecturer);
        var lecturer = new UserAccount("lecturer@example.com", role.Id);
        _db.Users.Add(lecturer);

        var bank = new QuestionBank("SE Bank", "Software Engineering Bank");
        _db.QuestionBanks.Add(bank);
        _db.SaveChanges();

        _bankId = bank.Id;
        _lecturerId = lecturer.Id;

        _service = new PostgresRubricService(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetRubricAsync_WhenBankDoesNotExist_ReturnsBankNotFound()
    {
        var result = await _service.GetRubricAsync(Guid.NewGuid());
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RubricServiceError.BankNotFound);
    }

    [Fact]
    public async Task GetRubricAsync_WhenRubricDoesNotExist_ReturnsRubricNotFound()
    {
        var result = await _service.GetRubricAsync(_bankId);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RubricServiceError.RubricNotFound);
    }

    [Fact]
    public async Task CreateRubricAsync_WhenValid_ReturnsCreatedRubric()
    {
        var request = new CreateRubricRequest("Grading Rubric", "Description");
        var result = await _service.CreateRubricAsync(_bankId, request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.QuestionBankId.Should().Be(_bankId);
        result.Value.Name.Should().Be("Grading Rubric");
        result.Value.Description.Should().Be("Description");

        var getResult = await _service.GetRubricAsync(_bankId);
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value!.Id.Should().Be(result.Value.Id);
    }

    [Fact]
    public async Task CreateRubricAsync_WhenBankDoesNotExist_ReturnsBankNotFound()
    {
        var request = new CreateRubricRequest("Grading Rubric");
        var result = await _service.CreateRubricAsync(Guid.NewGuid(), request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RubricServiceError.BankNotFound);
    }

    [Fact]
    public async Task CreateRubricAsync_WhenRubricAlreadyExists_ReturnsRubricAlreadyExists()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("First Rubric"));

        var duplicateResult = await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Second Rubric"));

        duplicateResult.IsSuccess.Should().BeFalse();
        duplicateResult.Error.Should().Be(RubricServiceError.RubricAlreadyExists);
    }

    [Theory]
    [InlineData(ExamSession.Statuses.Open)]
    [InlineData(ExamSession.Statuses.Closed)]
    public async Task CreateRubricAsync_WhenBankLockedByOpenOrClosedSession_ReturnsPreparationLocked(string status)
    {
        var session = new ExamSession("Session", _bankId, status, _lecturerId);
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        var result = await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric Name"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RubricServiceError.PreparationLocked);
    }

    [Fact]
    public async Task CreateRubricAsync_WhenBankHasOnlyDraftSession_Succeeds()
    {
        var session = new ExamSession("Draft Session", _bankId, ExamSession.Statuses.Draft, _lecturerId);
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        var result = await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Draft Bank Rubric"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRubricAsync_WhenValid_UpdatesRubric()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Initial Name", "Initial Desc"));

        var updateResult = await _service.UpdateRubricAsync(_bankId, new UpdateRubricRequest("Updated Name", "Updated Desc"));

        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value!.Name.Should().Be("Updated Name");
        updateResult.Value.Description.Should().Be("Updated Desc");

        var getResult = await _service.GetRubricAsync(_bankId);
        getResult.Value!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateRubricAsync_WhenRubricDoesNotExist_ReturnsRubricNotFound()
    {
        var result = await _service.UpdateRubricAsync(_bankId, new UpdateRubricRequest("Name"));
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RubricServiceError.RubricNotFound);
    }

    [Fact]
    public async Task UpdateRubricAsync_WhenBankLocked_ReturnsPreparationLocked()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Initial Name"));

        var session = new ExamSession("Locked Session", _bankId, ExamSession.Statuses.Open, _lecturerId);
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        var result = await _service.UpdateRubricAsync(_bankId, new UpdateRubricRequest("New Name"));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RubricServiceError.PreparationLocked);
    }

    [Fact]
    public async Task DeleteRubricAsync_WhenValidAndNoCriteria_DeletesRubric()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("To Delete"));

        var deleteResult = await _service.DeleteRubricAsync(_bankId);

        deleteResult.IsSuccess.Should().BeTrue();
        deleteResult.Value.Should().BeTrue();

        var getResult = await _service.GetRubricAsync(_bankId);
        getResult.Error.Should().Be(RubricServiceError.RubricNotFound);
    }

    [Fact]
    public async Task DeleteRubricAsync_WhenRubricHasCriteria_ReturnsRubricHasCriteria()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric With Criteria"));
        await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("Criterion 1", null, 10m, 0.5m));

        var deleteResult = await _service.DeleteRubricAsync(_bankId);

        deleteResult.IsSuccess.Should().BeFalse();
        deleteResult.Error.Should().Be(RubricServiceError.RubricHasCriteria);
    }

    [Fact]
    public async Task DeleteRubricAsync_WhenBankLocked_ReturnsPreparationLocked()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric To Lock"));

        var session = new ExamSession("Locked Session", _bankId, ExamSession.Statuses.Closed, _lecturerId);
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        var result = await _service.DeleteRubricAsync(_bankId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RubricServiceError.PreparationLocked);
    }

    [Fact]
    public async Task GetCriteriaAsync_WhenRubricDoesNotExist_ReturnsRubricNotFound()
    {
        var result = await _service.GetCriteriaAsync(_bankId);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RubricServiceError.RubricNotFound);
    }

    [Fact]
    public async Task GetCriteriaAsync_WhenRubricEmpty_ReturnsEmptyList()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Empty Rubric"));

        var result = await _service.GetCriteriaAsync(_bankId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateCriterionAsync_WhenValid_CreatesCriterion()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric"));

        var request = new CreateRubricCriterionRequest("Accuracy", "Technical accuracy", 10.00m, 0.4000m);
        var result = await _service.CreateCriterionAsync(_bankId, request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Accuracy");
        result.Value.Description.Should().Be("Technical accuracy");
        result.Value.MaxScore.Should().Be(10.00m);
        result.Value.Weight.Should().Be(0.4000m);

        var listResult = await _service.GetCriteriaAsync(_bankId);
        listResult.Value.Should().ContainSingle(c => c.Id == result.Value.Id);
    }

    [Fact]
    public async Task CreateCriterionAsync_AllowsSumOfWeightsNotEqualToOneDuringDrafting()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Draft Rubric"));

        var c1 = await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("C1", null, 10m, 0.2500m));
        var c2 = await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("C2", null, 10m, 0.2500m));

        c1.IsSuccess.Should().BeTrue();
        c2.IsSuccess.Should().BeTrue();

        var list = await _service.GetCriteriaAsync(_bankId);
        list.Value.Should().HaveCount(2);
        list.Value!.Sum(c => c.Weight).Should().Be(0.5000m);
    }

    [Fact]
    public async Task CreateCriterionAsync_WhenDuplicateNameInSameRubric_ReturnsCriterionNameExists()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric"));

        await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("Criterion A", null, 10m, 0.5m));

        var duplicateResult = await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("Criterion A", null, 5m, 0.5m));

        duplicateResult.IsSuccess.Should().BeFalse();
        duplicateResult.Error.Should().Be(RubricServiceError.CriterionNameExists);
    }

    [Fact]
    public async Task CreateCriterionAsync_WhenBankLocked_ReturnsPreparationLocked()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric"));

        var session = new ExamSession("Locked Session", _bankId, ExamSession.Statuses.Open, _lecturerId);
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        var result = await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("Criterion", null, 10m, 0.5m));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RubricServiceError.PreparationLocked);
    }

    [Fact]
    public async Task GetCriterionByIdAsync_WhenBelongsToRubric_ReturnsCriterion()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric"));
        var created = await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("Criterion A", null, 10m, 0.5m));

        var result = await _service.GetCriterionByIdAsync(_bankId, created.Value!.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Criterion A");
    }

    [Fact]
    public async Task GetCriterionByIdAsync_WhenBelongsToDifferentRubric_ReturnsCriterionNotFound()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric 1"));
        var createdInBank1 = await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("Crit 1", null, 10m, 0.5m));

        // Create Bank 2 with Rubric 2
        var bank2 = new QuestionBank("Bank 2");
        _db.QuestionBanks.Add(bank2);
        await _db.SaveChangesAsync();
        await _service.CreateRubricAsync(bank2.Id, new CreateRubricRequest("Rubric 2"));

        // Query bank 2 for bank 1's criterion
        var result = await _service.GetCriterionByIdAsync(bank2.Id, createdInBank1.Value!.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(RubricServiceError.CriterionNotFound);
    }

    [Fact]
    public async Task UpdateCriterionAsync_WhenValid_UpdatesAllFields()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric"));
        var created = await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("Old Name", "Old Desc", 10m, 0.5m));

        var updateRequest = new UpdateRubricCriterionRequest("New Name", "New Desc", 20.00m, 0.8000m);
        var updateResult = await _service.UpdateCriterionAsync(_bankId, created.Value!.Id, updateRequest);

        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value!.Name.Should().Be("New Name");
        updateResult.Value.Description.Should().Be("New Desc");
        updateResult.Value.MaxScore.Should().Be(20.00m);
        updateResult.Value.Weight.Should().Be(0.8000m);
    }

    [Fact]
    public async Task UpdateCriterionAsync_WhenKeepingSameName_Succeeds()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric"));
        var created = await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("Same Name", "Old Desc", 10m, 0.5m));

        var updateRequest = new UpdateRubricCriterionRequest("Same Name", "Updated Desc", 15m, 0.7m);
        var updateResult = await _service.UpdateCriterionAsync(_bankId, created.Value!.Id, updateRequest);

        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value!.Description.Should().Be("Updated Desc");
    }

    [Fact]
    public async Task UpdateCriterionAsync_WhenRenamingToExistingNameInRubric_ReturnsCriterionNameExists()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric"));
        await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("Existing Name", null, 10m, 0.3m));
        var second = await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("Second", null, 10m, 0.3m));

        var updateRequest = new UpdateRubricCriterionRequest("Existing Name", null, 10m, 0.5m);
        var updateResult = await _service.UpdateCriterionAsync(_bankId, second.Value!.Id, updateRequest);

        updateResult.IsSuccess.Should().BeFalse();
        updateResult.Error.Should().Be(RubricServiceError.CriterionNameExists);
    }

    [Fact]
    public async Task UpdateCriterionAsync_WhenBankLocked_ReturnsPreparationLocked()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric"));
        var created = await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("Criterion", null, 10m, 0.5m));

        var session = new ExamSession("Locked Session", _bankId, ExamSession.Statuses.Open, _lecturerId);
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        var updateResult = await _service.UpdateCriterionAsync(_bankId, created.Value!.Id, new UpdateRubricCriterionRequest("Renamed", null, 10m, 0.5m));

        updateResult.IsSuccess.Should().BeFalse();
        updateResult.Error.Should().Be(RubricServiceError.PreparationLocked);
    }

    [Fact]
    public async Task DeleteCriterionAsync_WhenValid_DeletesCriterion()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric"));
        var created = await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("To Delete", null, 10m, 0.5m));

        var deleteResult = await _service.DeleteCriterionAsync(_bankId, created.Value!.Id);

        deleteResult.IsSuccess.Should().BeTrue();

        var getResult = await _service.GetCriterionByIdAsync(_bankId, created.Value.Id);
        getResult.IsSuccess.Should().BeFalse();
        getResult.Error.Should().Be(RubricServiceError.CriterionNotFound);
    }

    [Fact]
    public async Task DeleteCriterionAsync_WhenBankLocked_ReturnsPreparationLocked()
    {
        await _service.CreateRubricAsync(_bankId, new CreateRubricRequest("Rubric"));
        var created = await _service.CreateCriterionAsync(_bankId, new CreateRubricCriterionRequest("Criterion", null, 10m, 0.5m));

        var session = new ExamSession("Locked Session", _bankId, ExamSession.Statuses.Closed, _lecturerId);
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        var deleteResult = await _service.DeleteCriterionAsync(_bankId, created.Value!.Id);

        deleteResult.IsSuccess.Should().BeFalse();
        deleteResult.Error.Should().Be(RubricServiceError.PreparationLocked);
    }
}
