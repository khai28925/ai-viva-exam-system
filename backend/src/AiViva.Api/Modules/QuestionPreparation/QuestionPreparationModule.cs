using AiViva.Api.Endpoints;
using AiViva.Api.Modules.QuestionPreparation.Wf1;

namespace AiViva.Api.Modules.QuestionPreparation;

public static class QuestionPreparationModule
{
    public static RouteGroupBuilder MapQuestionPreparationModule(this RouteGroupBuilder api)
    {
        api.MapQuestionBankEndpoints();
        api.MapExpectedKeyEndpoints();
        api.MapRubricEndpoints();
        api.MapWf1Endpoints();
        return api;
    }
}
