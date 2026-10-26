using AiViva.Api.Endpoints;

namespace AiViva.Api.Modules.Viva;

public static class VivaModule
{
    public static RouteGroupBuilder MapVivaModule(this RouteGroupBuilder api)
    {
        api.MapExamSessionEndpoints();
        return api;
    }
}
