using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Scutum.T10.Data;

public static class DependencyInjection
{
    // Konekcija dolazi iz ConnectionStrings:T10 (compose: promenljiva ConnectionStrings__T10;
    // lokalno: dotnet user-secrets). Proverava se tek kada se DbContext prvi put zatraži.
    public static IServiceCollection AddT10Data(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<T10DbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("T10")
                ?? throw new InvalidOperationException("Nedostaje ConnectionStrings:T10."))
            .UseSnakeCaseNamingConvention());

        // Ovde se registruju klijenti ka drugim timovima (External/).
        return services;
    }
}
