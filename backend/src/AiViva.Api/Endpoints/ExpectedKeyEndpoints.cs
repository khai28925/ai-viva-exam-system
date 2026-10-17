using System.Security.Claims;
using AiViva.Api.Extensions;
using AiViva.Application.ExamSessions;

namespace AiViva.Api.Endpoints;

public static class ExpectedKeyEndpoints
{
    public static RouteGroupBuilder MapExpectedKeyEndpoints(this RouteGroupBuilder group)
    {
        var sessions = group.MapGroup("/api/v1/question-banks/{bankId:guid}/questions/{questionId:guid}/expected-key-points").WithTags("Question Banks")
            .WithTags("ExpectedKeyPoints")
            .RequireAuthorization(AuthPolicies.QuestionBankManage);
        return group;
    }
}
