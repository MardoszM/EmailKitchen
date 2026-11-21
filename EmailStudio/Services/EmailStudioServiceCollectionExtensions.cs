using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EmailStudio.Services;

public static class EmailStudioServiceCollectionExtensions
{
    public static IServiceCollection AddEmailStudio(this IServiceCollection services)
    {
        services.AddRazorPages().AddApplicationPart(typeof(EmailStudioServiceCollectionExtensions).Assembly);
        services.TryAddScoped<IRazorViewRendererService, RazorViewRendererService>();
        return services;
    }
}
