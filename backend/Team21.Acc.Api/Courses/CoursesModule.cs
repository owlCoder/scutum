using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Team21.Acc.Api.Courses;

/// <summary>
/// ACC-01 services and endpoints are registered here.
/// </summary>
public static class CoursesModule
{
    public static IServiceCollection AddCourses(this IServiceCollection services)
    {
        // services.AddScoped<ICourseService, CourseService>();
        return services;
    }

    public static IEndpointRouteBuilder MapCourses(this IEndpointRouteBuilder app)
    {
        // app.MapCourseEndpoints();   // routes under /api/courses
        return app;
    }
}
