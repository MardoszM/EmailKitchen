using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EmailStudio.Services;

/// <summary>
/// Provides extension methods for registering Email Studio services.
/// </summary>
public static class EmailStudioServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Razor services and default view renderer used by Email Studio.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <returns>The same service collection so that additional registrations can be chained.</returns>
    /// <remarks>
    /// An existing <see cref="IRazorViewRendererService"/> registration is preserved.
    /// To enable sending test emails, register an implementation of <see cref="IEmailSender"/> separately.
    /// Map the Studio endpoints with <see cref="EmailStudio.Studio.EmailStudioEndpoints.MapEmailStudio"/>
    /// after building the application.
    /// </remarks>
    public static IServiceCollection AddEmailStudio(this IServiceCollection services)
    {
        services.AddRazorPages().AddApplicationPart(typeof(EmailStudioServiceCollectionExtensions).Assembly);
        services.TryAddScoped<IRazorViewRendererService, RazorViewRendererService>();
        return services;
    }
}
