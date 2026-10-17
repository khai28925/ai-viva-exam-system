using System.Threading.Tasks;
using AiViva.Api.Endpoints;
using AiViva.Api.Extensions;
using AiViva.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiViva.Tests;

public sealed class RubricEndpointTests
{
    [Fact]
    public async Task QuestionBankManagePolicyAllowsLecturerAndAdminRejectsStudent()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationBuilder()
            .AddPolicy(
                AuthPolicies.QuestionBankManage,
                policy => policy.RequireRole(Role.Codes.Admin, Role.Codes.Lecturer));

        using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync(AuthPolicies.QuestionBankManage);

        policy.Should().NotBeNull();
        var roleRequirement = policy!.Requirements
            .OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>()
            .Single();

        roleRequirement.AllowedRoles.Should().Contain(Role.Codes.Lecturer);
        roleRequirement.AllowedRoles.Should().Contain(Role.Codes.Admin);
        roleRequirement.AllowedRoles.Should().NotContain(Role.Codes.Student);
    }

    [Fact]
    public void MapRubricEndpointsRegistersExpectedRoutesAndMetadata()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddEndpointsApiExplorer();
        var app = builder.Build();

        var group = app.MapGroup("/api");
        group.MapRubricEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

        endpoints.Should().Contain(e => e.RoutePattern.RawText != null && e.RoutePattern.RawText.Trim('/') == "api/v1/question-banks/{bankId:guid}/rubric");
        endpoints.Should().Contain(e => e.RoutePattern.RawText != null && e.RoutePattern.RawText.Trim('/') == "api/v1/question-banks/{bankId:guid}/rubric/criteria");
        endpoints.Should().Contain(e => e.RoutePattern.RawText != null && e.RoutePattern.RawText.Trim('/') == "api/v1/question-banks/{bankId:guid}/rubric/criteria/{criterionId:guid}");

        var rubricEndpoints = endpoints
            .Where(e => e.RoutePattern.RawText?.Trim('/').StartsWith("api/v1/question-banks/{bankId:guid}/rubric") == true)
            .ToArray();

        rubricEndpoints.Should().NotBeEmpty();
        foreach (var endpoint in rubricEndpoints)
        {
            var authData = endpoint.Metadata.OfType<IAuthorizeData>().ToArray();
            authData.Should().Contain(a => a.Policy == AuthPolicies.QuestionBankManage);
        }
    }
}
