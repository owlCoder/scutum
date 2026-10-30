namespace Platform.Api.Modules.Laboratories;

public static class LaboratoriesModule
{
    public static IServiceCollection AddLaboratories(this IServiceCollection services)
    {
        return services;
    }

    public static IEndpointRouteBuilder MapLaboratories(this IEndpointRouteBuilder app)
    {
        return app;
    }
}