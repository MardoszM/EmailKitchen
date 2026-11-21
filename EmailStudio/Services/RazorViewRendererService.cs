using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace EmailStudio.Services;

public sealed class RazorViewRendererService(
    IRazorViewEngine viewEngine,
    ITempDataProvider tempDataProvider,
    IServiceProvider serviceProvider) : IRazorViewRendererService
{
    public async Task<string> RenderViewToStringAsync<TModel>(string viewName, TModel model,
        CancellationToken ct = default)
    {
        var actionContext = GetActionContext(ct);
        var view = FindView(actionContext, viewName);

        await using var output = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            view,
            new ViewDataDictionary<TModel>(
                metadataProvider: new EmptyModelMetadataProvider(),
                modelState: new ModelStateDictionary())
            {
                Model = model
            },
            new TempDataDictionary(actionContext.HttpContext, tempDataProvider),
            output,
            new HtmlHelperOptions());

        await view.RenderAsync(viewContext);
        return output.ToString();
    }

    private IView FindView(ActionContext actionContext, string viewName)
    {
        if (!viewName.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            viewName = $"{viewName}.cshtml";

        var getViewResult = viewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: true);
        if (getViewResult.Success)
            return getViewResult.View;

        var findViewResult = viewEngine.FindView(actionContext, viewName, isMainPage: true);
        if (findViewResult.Success)
            return findViewResult.View;

        var searchedLocations = getViewResult.SearchedLocations.Concat(findViewResult.SearchedLocations);
        var errorMessage = string.Join(
            Environment.NewLine,
            new[] { $"Unable to find view '{viewName}'. The following locations were searched:" }.Concat(
                searchedLocations));

        throw new InvalidOperationException(errorMessage);
    }

    private ActionContext GetActionContext(CancellationToken ct)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            RequestAborted = ct
        };
        return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
    }
}