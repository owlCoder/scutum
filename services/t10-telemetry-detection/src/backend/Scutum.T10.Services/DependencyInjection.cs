using Microsoft.Extensions.DependencyInjection;

namespace Scutum.T10.Services;

public static class DependencyInjection
{
    // Ovde se registruju servisi po celini (Sources/, Ingestion/, Detection/).
    public static IServiceCollection AddT10Services(this IServiceCollection services)
    {
        return services;
    }
}
