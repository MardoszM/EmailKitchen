namespace EmailKitchen.Services;

/// <summary>
/// Renders Razor views and their layouts into HTML using a supplied model.
/// </summary>
public interface IRazorViewRendererService
{
    /// <summary>
    /// Renders a Razor view and its layout into an HTML string.
    /// </summary>
    /// <typeparam name="TModel">The type of model passed to the view.</typeparam>
    /// <param name="viewPath">
    /// The path to the Razor view from the application root,
    /// for example <c>~/EmailKitchen/Templates/Welcome.cshtml</c>.
    /// Include the directories leading to the view and start the path with <c>~/</c> or <c>/</c>.
    /// The <c>.cshtml</c> extension can be omitted when using the default renderer.
    /// </param>
    /// <param name="model">The model made available to the view and its layout.</param>
    /// <param name="ct">The cancellation token associated with the rendering request.</param>
    /// <returns>A task whose result is the rendered HTML, including any layout output.</returns>
    /// <exception cref="InvalidOperationException">The requested view cannot be found.</exception>
    Task<string> RenderViewToStringAsync<TModel>(string viewPath, TModel model, CancellationToken ct = default);
}
