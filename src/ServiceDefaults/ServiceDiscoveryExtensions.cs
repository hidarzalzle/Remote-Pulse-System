using Microsoft.Extensions.DependencyInjection;

namespace ServiceDefaults;

public static class ServiceDiscoveryExtensions
{
    // Placeholder service discovery registration.
    // The original repo likely depends on a library providing this.
    // To keep compilation working, provide a no-op registration.
    public static IServiceCollection AddServiceDiscovery(this IServiceCollection services)
    {
        return services;
    }
}
