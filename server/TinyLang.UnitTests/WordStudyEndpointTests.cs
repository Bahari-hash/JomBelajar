using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TinyLang.Constants;
using TinyLang.Endpoints;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class WordStudyEndpointTests
{
    [Fact]
    public void LearningAndReviewRoutesShouldBeSeparateAndRequireUsers()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Mock.Of<IWordStudyService>());
        using var app = builder.Build();
        app.MapGroup("/api").MapWordStudyApi();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Contains("word-study") == true)
            .ToArray();

        routes.Should().HaveCount(15);
        routes.Should().OnlyContain(route => route.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Any(value => value.Policy == AuthorizationPolicies.RequireUser));
        routes.Select(route => route.RoutePattern.RawText)
            .Should().Contain(path => path!.Contains("/word-study/learning/"));
        routes.Select(route => route.RoutePattern.RawText)
            .Should().Contain(path => path!.Contains("/word-study/review/"));
        routes.Select(route => route.RoutePattern.RawText)
            .Should().Contain("/api/word-study/review/today");
        routes.Select(route => route.RoutePattern.RawText)
            .Should().Contain("/api/word-study/check-ins");
    }
}
