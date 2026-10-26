using AiViva.Api.Endpoints;

namespace AiViva.Api.Modules.Identity;

public static class IdentityModule
{
    public static RouteGroupBuilder MapIdentityModule(this RouteGroupBuilder api)
    {
        api.MapAuthEndpoints();
        api.MapAdminUserEndpoints();
        return api;
    }
}
