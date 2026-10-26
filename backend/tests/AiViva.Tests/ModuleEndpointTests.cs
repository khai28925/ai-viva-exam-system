using AiViva.Api.Modules;
using AiViva.Application;
using AiViva.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AiViva.Tests;

public sealed class ModuleEndpointTests
{
    [Fact]
    public void ModuleCompositionMapsEachExistingApiArea()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["ConnectionStrings:QuestionBank"] =
            "Host=localhost;Database=aives;Username=aives";
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddAntiforgery();
        builder.Services.AddEndpointsApiExplorer();
        var app = builder.Build();

        app.MapGroup("/api").MapApiModules();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
        var routes = endpoints.Select(endpoint => endpoint.RoutePattern.RawText?.Trim('/')).ToArray();

        routes.Should().Contain("api/health");
        routes.Should().Contain("api/v1/auth/login");
        routes.Should().Contain("api/v1/admin/users");
        routes.Should().Contain("api/v1/question-banks");
        routes.Should().Contain("api/v1/question-banks/{bankId}/questions");
        routes.Should().Contain("api/v1/question-banks/{bankId:guid}/questions/{questionId:guid}/expected-key-points");
        routes.Should().Contain("api/v1/question-banks/{bankId:guid}/rubric");
        routes.Should().Contain("api/v1/exam-sessions/{id:guid}/join");
        routes.Should().Contain("api/v1/exams");

        var join = endpoints.Single(endpoint => endpoint.RoutePattern.RawText?.Trim('/') ==
            "api/v1/exam-sessions/{id:guid}/join");
        join.Metadata.OfType<IHttpMethodMetadata>().Single().HttpMethods.Should().Contain("POST");
    }
}
