using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.Runtime;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ServiceDefaults;

public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : class
    {
        var services = GetServicesFromBuilder(builder);

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());

        services.AddHealthChecks();
        services.AddServiceDiscovery();

        return builder;
    }

    public static TApp MapDefaultEndpoints<TApp>(this TApp app)
        where TApp : class
    {
        if (app == null) throw new ArgumentNullException(nameof(app));

        // MapHealthChecks is an extension method, so we can't reliably invoke it via reflection.
        // Instead, map `/health` for the common ASP.NET Core pipeline types we use.
        switch (app)
        {
            case WebApplication webApp:
                webApp.MapHealthChecks("/health");
                break;
            case IEndpointRouteBuilder endpoints:
                endpoints.MapHealthChecks("/health");
                break;
        }

        return app;
    }

    private static IServiceCollection GetServicesFromBuilder(object builder)
    {
        if (builder == null) throw new ArgumentNullException(nameof(builder));

        var prop = builder.GetType().GetProperty("Services");
        if (prop?.GetValue(builder) is IServiceCollection services)
            return services;

        throw new InvalidOperationException("Could not locate an IServiceCollection 'Services' property on the provided builder.");
    }
}
