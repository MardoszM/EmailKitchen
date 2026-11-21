namespace EmailStudio.Services;

public interface IRazorViewRendererService
{
    Task<string> RenderViewToStringAsync<TModel>(string viewName, TModel model, CancellationToken ct = default);
}