using AiViva.Api.Endpoints;

namespace AiViva.Api.Modules.QuestionPreparation;

public static class QuestionPreparationModule
{
    public static RouteGroupBuilder MapQuestionPreparationModule(this RouteGroupBuilder api)
    {
        api.MapQuestionBankEndpoints();
        api.MapExpectedKeyEndpoints();
        api.MapRubricEndpoints();
        return api;
    }
}
