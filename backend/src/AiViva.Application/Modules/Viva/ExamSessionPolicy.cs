using AiViva.Domain.Entities;
using AiViva.ModuleContracts.QuestionPreparation;

namespace AiViva.Application.ExamSessions;

public enum ExamSessionUpdateKind { Unchanged, EditDraft, Publish, Close, Invalid }

public sealed record ExamSessionReadiness(
    IReadOnlyCollection<string> Missing, IReadOnlyCollection<Guid> QuestionIdsWithoutKeyPoints)
{
    public bool IsReady => Missing.Count == 0;
}

public static class ExamSessionPolicy
{
    public static bool CanCreate(string status) => status == ExamSession.Statuses.Draft;

    public static bool CanEditSelection(string status) => status == ExamSession.Statuses.Draft;

    public static bool CanDelete(string status, bool hasAttempts) =>
        status == ExamSession.Statuses.Draft && !hasAttempts;

    public static bool CanJoin(string status) => status == ExamSession.Statuses.Open;

    public static ExamSessionUpdateKind ClassifyUpdate(ExamSession session, SaveExamSessionRequest request)
    {
        if (session.Title == request.Title && session.QuestionBankId == request.QuestionBankId &&
            session.Status == request.Status)
            return ExamSessionUpdateKind.Unchanged;

        if (session.Status == ExamSession.Statuses.Draft && request.Status == ExamSession.Statuses.Draft)
            return ExamSessionUpdateKind.EditDraft;

        if (session.Status == ExamSession.Statuses.Draft && request.Status == ExamSession.Statuses.Open &&
            session.QuestionBankId == request.QuestionBankId)
            return ExamSessionUpdateKind.Publish;

        if (session.Status == ExamSession.Statuses.Open && request.Status == ExamSession.Statuses.Closed &&
            session.QuestionBankId == request.QuestionBankId && session.Title == request.Title)
            return ExamSessionUpdateKind.Close;

        return ExamSessionUpdateKind.Invalid;
    }

    public static bool IsValidSelection(IReadOnlyList<Guid>? questionIds) =>
        questionIds is not null && questionIds.Count <= 50 &&
        questionIds.All(questionId => questionId != Guid.Empty) &&
        questionIds.Distinct().Count() == questionIds.Count;

    public static ExamSessionReadiness CheckReadiness(
        IReadOnlyCollection<Guid> selectedIds, QuestionPreparationReadiness preparation)
    {
        var missing = new List<string>();
        if (selectedIds.Count == 0) missing.Add("selection");

        var withKeyPoints = preparation.QuestionIdsWithKeyPoints.ToHashSet();
        var withoutKeyPoints = selectedIds.Where(id => !withKeyPoints.Contains(id)).ToArray();
        if (withoutKeyPoints.Length > 0) missing.Add("keyPoints");
        if (preparation.ValidQuestionIds.Count != selectedIds.Count && !missing.Contains("selection"))
            missing.Add("selection");

        if (!preparation.HasRubric) missing.Add("rubric");
        else if (preparation.CriterionWeights.Count == 0) missing.Add("rubricCriteria");
        else if (preparation.CriterionWeights.Sum() != 1.0000m) missing.Add("rubricWeight");

        return new ExamSessionReadiness(missing, withoutKeyPoints);
    }
}
