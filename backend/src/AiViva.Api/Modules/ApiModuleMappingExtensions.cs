using AiViva.Api.Modules.Identity;
using AiViva.Api.Modules.LegacyExams;
using AiViva.Api.Modules.Operations;
using AiViva.Api.Modules.QuestionPreparation;
using AiViva.Api.Modules.Viva;

namespace AiViva.Api.Modules;

public static class ApiModuleMappingExtensions
{
    public static RouteGroupBuilder MapApiModules(this RouteGroupBuilder api)
    {
        api.MapOperationsModule();
        api.MapIdentityModule();
        api.MapQuestionPreparationModule();
        api.MapVivaModule();
        api.MapLegacyExamsModule();
        return api;
    }
}
