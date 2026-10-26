using AiViva.Api.Endpoints;

namespace AiViva.Api.Modules.LegacyExams;

public static class LegacyExamsModule
{
    public static RouteGroupBuilder MapLegacyExamsModule(this RouteGroupBuilder api)
    {
        api.MapExamEndpoints();
        return api;
    }
}
