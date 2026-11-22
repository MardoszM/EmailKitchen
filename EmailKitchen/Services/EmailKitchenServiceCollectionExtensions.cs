using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EmailKitchen.Services;

/// <summary>
/// Provides extension methods for registering Email Kitchen services.
/// </summary>
public static class EmailKitchenServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Razor services, default view renderer, and JSON email translator used by Email Kitchen.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <returns>The same service collection so that additional registrations can be chained.</returns>
    /// <remarks>
    /// An existing <see cref="IRazorViewRendererService"/> registration is preserved.
    /// An existing <see cref="IEmailTranslator"/> registration is also preserved.
    /// To enable sending test emails, register an implementation of <see cref="IEmailSender"/> separately.
    /// Map the Kitchen endpoints with <see cref="EmailKitchen.Kitchen.EmailKitchenEndpoints.MapEmailKitchen"/>
    /// after building the application.
    /// </remarks>
    public static IServiceCollection AddEmailKitchen(this IServiceCollection services)
    {
        services.AddRazorPages().AddApplicationPart(typeof(EmailKitchenServiceCollectionExtensions).Assembly);
        services.TryAddScoped<IRazorViewRendererService, RazorViewRendererService>();
        services.TryAddSingleton<IEmailTranslator, JsonEmailTranslator>();
        return services;
    }
}
