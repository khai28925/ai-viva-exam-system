using AiViva.Api.Endpoints;

namespace AiViva.Api.Modules.Operations;

public static class OperationsModule
{
    public static RouteGroupBuilder MapOperationsModule(this RouteGroupBuilder api)
    {
        api.MapHealthEndpoints();
        return api;
    }
}
