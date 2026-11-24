using System.Net.Mail;
using System.Text.Json;
using System.Reflection;
using EmailKitchen.Services;
using Microsoft.AspNetCore.Http;
using System.Text.Encodings.Web;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Razor;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.DependencyInjection;

namespace EmailKitchen.Kitchen;

/// <summary>
/// Provides extension methods for adding the Email Kitchen interface and HTTP endpoints to an application.
/// </summary>
public static class EmailKitchenEndpoints
{
    private const string Route = "/email-kitchen";
    private const string DefaultTemplatesPath = "./EmailKitchen/Templates";

    private static readonly (string Resource, string RelativePath)[] DefaultTemplates =
    [
        ("EmailKitchen.DefaultTemplates.Shared.Layout.cshtml", "Shared/Layout.cshtml"),
        ("EmailKitchen.DefaultTemplates.Shared.Layout.cshtml.cs", "Shared/Layout.cshtml.cs"),
        ("EmailKitchen.DefaultTemplates.TestEmail.cshtml", "TestEmail.cshtml"),
        ("EmailKitchen.DefaultTemplates.TestEmail.cshtml.cs", "TestEmail.cshtml.cs"),
        ("EmailKitchen.DefaultTemplates.SupportedLanguages.cs", "SupportedLanguages.cs"),
        ("EmailKitchen.DefaultTemplates.Translations.eng.json", "Translations/eng.json")
    ];

    private static readonly JsonSerializerOptions ModelOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Maps the Email Kitchen interface, assets, template previews, rendering, and test email endpoints
    /// under <c>/email-kitchen</c>.
    /// </summary>
    /// <param name="app">The application in which to map the Kitchen endpoints.</param>
    /// <returns>The route group containing all Kitchen endpoints, allowing authorization policies and other endpoint conventions to be applied.</returns>
    /// <remarks>
    /// Register the required services with
    /// <see cref="EmailKitchenServiceCollectionExtensions.AddEmailKitchen"/> before building the application.
    /// The <c>EmailKitchenTemplatesPath</c> MSBuild property selects the template directory relative to the
    /// application's content root; the default is <c>./EmailKitchen/Templates</c>.
    /// Default template files are created if the directory contains no Razor templates.
    /// This method maps endpoints whenever it is called, so call it inside a development environment check
    /// if Kitchen should only be available during development.
    /// Use <c>RequireAuthorization</c> on the returned group to protect the interface, assets, and API endpoints
    /// with the host application's authorization policy.
    /// Sending test emails requires a registered <see cref="IEmailSender"/> and a valid antiforgery token.
    /// </remarks>
    /// <exception cref="ArgumentException">The configured template directory is outside the application's content root.</exception>
    /// <exception cref="InvalidOperationException">An embedded default template resource is missing.</exception>
    /// <exception cref="IOException">The default template files cannot be created or written.</exception>
    /// <exception cref="UnauthorizedAccessException">The application cannot access the template directory.</exception>
    /// <example>
    /// <code>
    /// if (app.Environment.IsDevelopment())
    /// {
    ///     app.MapEmailKitchen();
    /// }
    /// </code>
    /// <para>To allow authorized remote access using a policy configured by the host:</para>
    /// <code>
    /// app.MapEmailKitchen().RequireAuthorization("EmailKitchenAccess");
    /// </code>
    /// </example>
    public static RouteGroupBuilder MapEmailKitchen(this WebApplication app)
    {
        var templatesPath = GetConfiguredTemplatesPath();
        var useProjectRoot = templatesPath == ".";
        var (templatesDirectory, relativeTemplatesPath) = ResolveTemplatesPath(app.Environment.ContentRootPath,
            useProjectRoot ? "." : templatesPath!);
        CreateDefaultTemplatesIfMissing(templatesDirectory);

        var kitchen = app.MapGroup(Route);
        kitchen.MapGet("", (HttpContext context, IAntiforgery antiforgery) =>
        {
            using var reader = new StreamReader(OpenAsset("kitchen.html"));
            var token = HtmlEncoder.Default.Encode(antiforgery.GetAndStoreTokens(context).RequestToken!);
            var html = ReplaceKitchenRoot(reader.ReadToEnd(), GetKitchenRoot(context));
            return Results.Content(html.Replace("__ANTIFORGERY_TOKEN__", token),
                "text/html; charset=utf-8");
        });
        kitchen.MapGet("/assets/{asset}", (string asset, HttpContext context) => asset switch
        {
            "kitchen.css" => ServeAsset(asset, "text/css; charset=utf-8"),
            "kitchen.js" => ServeAsset(asset, "text/javascript; charset=utf-8", GetKitchenRoot(context)),
            "client-preview.js" or "client-preview.worker.js" or "client-profiles.js" or "emailens-engine.js" =>
                ServeAsset(asset, "text/javascript; charset=utf-8"),
            "third-party-notices.txt" => ServeAsset(asset, "text/plain; charset=utf-8"),
            "email-kitchen-logo.png" => ServeLogo(context),
            "email-kitchen-icon.png" => ServeIcon(context),
            _ => Results.NotFound()
        });
        kitchen.MapGet("/templates", (IServiceProviderIsService services) =>
            Results.Ok(ReadCatalog(templatesDirectory, services.IsService(typeof(IEmailSender)))));
        kitchen.MapGet("/model/{**template}", (string template, IRazorViewEngine engine) =>
        {
            var path = GetTemplatePath(templatesDirectory, relativeTemplatesPath, template);
            if (path is null)
                return Results.NotFound();

            try
            {
                return Results.Json(new { model = CreateDefaultModel(GetModelType(engine, path)) }, ModelOptions);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        });
        kitchen.MapPost("/render", (KitchenRenderRequest request, HttpContext context, IRazorViewEngine engine,
                IRazorViewRendererService renderer, CancellationToken ct) =>
            RenderAsync(request, templatesDirectory, relativeTemplatesPath, GetKitchenRoot(context), engine, renderer,
                ct));
        kitchen.MapPost("/send", (KitchenSendRequest request, HttpContext context,
                IAntiforgery antiforgery, IRazorViewEngine engine, IRazorViewRendererService renderer,
                ILoggerFactory loggerFactory, CancellationToken ct) =>
            SendAsync(request, templatesDirectory, relativeTemplatesPath, context, antiforgery, engine, renderer,
                loggerFactory, ct));
        kitchen.MapGet("/preview/{**template}", (string template, HttpContext context) =>
            GetTemplatePath(templatesDirectory, relativeTemplatesPath, template) is null
                ? Results.NotFound()
                : ServeAsset("preview.html", "text/html; charset=utf-8", GetKitchenRoot(context)));

        return kitchen;
    }

    private static IResult ServeAsset(string name, string contentType) => Results.File(OpenAsset(name), contentType);

    private static IResult ServeAsset(string name, string contentType, string kitchenRoot)
    {
        using var reader = new StreamReader(OpenAsset(name));
        return Results.Content(ReplaceKitchenRoot(reader.ReadToEnd(), kitchenRoot), contentType);
    }

    private static IResult ServeLogo(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";

        return Results.File(EmailLogo.Open(), "image/png");
    }

    private static IResult ServeIcon(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";

        return Results.File(OpenAsset("email-kitchen-icon.png"), "image/png");
    }

    private static string GetKitchenRoot(HttpContext context) => $"{context.Request.PathBase}{Route}";

    private static string ReplaceKitchenRoot(string content, string kitchenRoot) =>
        content.Replace("__EMAIL_KITCHEN_ROOT__", kitchenRoot, StringComparison.Ordinal)
            .Replace("__EMAIL_KITCHEN_LOGO_URL__", GetLogoUrl(kitchenRoot), StringComparison.Ordinal)
            .Replace("__EMAIL_KITCHEN_ICON_URL__", GetIconUrl(kitchenRoot), StringComparison.Ordinal);

    private static string GetLogoUrl(string kitchenRoot)
    {
        using var logo = EmailLogo.Open();
        var version = Convert.ToHexStringLower(SHA256.HashData(logo));
        return $"{kitchenRoot}/assets/email-kitchen-logo.png?v={version}";
    }

    private static string GetIconUrl(string kitchenRoot)
    {
        using var icon = OpenAsset("email-kitchen-icon.png");
        var version = Convert.ToHexStringLower(SHA256.HashData(icon));
        return $"{kitchenRoot}/assets/email-kitchen-icon.png?v={version}";
    }

    private static Stream OpenAsset(string name) =>
        typeof(EmailKitchenEndpoints).Assembly.GetManifestResourceStream($"EmailKitchen.{name}")!;

    internal static string GetConfiguredTemplatesPath()
    {
        var configuredPath = Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "EmailKitchenTemplatesPath")?.Value;
        return string.IsNullOrWhiteSpace(configuredPath) ? DefaultTemplatesPath : configuredPath;
    }

    private static (string Directory, string RelativePath) ResolveTemplatesPath(string contentRoot,
        string templatesPath)
    {
        if (string.IsNullOrWhiteSpace(templatesPath))
            throw new ArgumentException("Provide a templates directory relative to the application root.",
                nameof(templatesPath));

        var root = Path.GetFullPath(contentRoot);
        var normalizedTemplatesPath = templatesPath.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var directory = Path.GetFullPath(Path.Combine(root, normalizedTemplatesPath));
        var relativePath = Path.GetRelativePath(root, directory);
        if (relativePath == ".." || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
            throw new ArgumentException("The templates directory must be inside the application root.",
                nameof(templatesPath));

        return (directory, relativePath == "." ? "" : relativePath.Replace(Path.DirectorySeparatorChar, '/'));
    }

    private static void CreateDefaultTemplatesIfMissing(string templatesDirectory)
    {
        if (Directory.Exists(templatesDirectory) &&
            Directory.EnumerateFiles(templatesDirectory, "*.cshtml", SearchOption.AllDirectories).Any())
            return;

        Directory.CreateDirectory(templatesDirectory);
        foreach (var (resourceName, relativePath) in DefaultTemplates)
        {
            var destination = Path.Combine(templatesDirectory, relativePath);
            if (File.Exists(destination))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var source = typeof(EmailKitchenEndpoints).Assembly.GetManifestResourceStream(resourceName)
                               ?? throw new InvalidOperationException(
                                   $"Default Email Kitchen template '{resourceName}' is missing.");
            using var target = File.Create(destination);
            source.CopyTo(target);
        }
    }

    private static object ReadCatalog(string directory, bool canSend)
    {
        var names = new List<string>();
        var revision = new HashCode();
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.cshtml", SearchOption.AllDirectories)
                         .Order(StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.ChangeExtension(Path.GetRelativePath(directory, file), null)
                    ?.Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/') ?? "";
                revision.Add(name, StringComparer.Ordinal);
                revision.Add(File.ReadAllText(file), StringComparer.Ordinal);
                if (!IsLayout(name))
                    names.Add(name);
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.cshtml.cs", SearchOption.AllDirectories)
                         .Concat(Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories))
                         .Order(StringComparer.OrdinalIgnoreCase))
            {
                revision.Add(Path.GetRelativePath(directory, file), StringComparer.Ordinal);
                revision.Add(File.ReadAllText(file), StringComparer.Ordinal);
            }
        }

        return new
        {
            Templates = names,
            Revision = revision.ToHashCode(),
            CanSend = canSend,
            IsRunningUnderDotnetWatch = Environment.GetEnvironmentVariable("DOTNET_WATCH") == "1"
        };
    }

    private static string? GetTemplatePath(string templatesDirectory, string relativeTemplatesPath,
        string? template)
    {
        if (string.IsNullOrWhiteSpace(template) || template.Contains('\\') || IsLayout(template) ||
            template.Split('/').Any(segment => segment is "" or "." or ".."))
            return null;

        var filePath = Path.Combine(templatesDirectory,
            $"{template.Replace('/', Path.DirectorySeparatorChar)}.cshtml");
        if (!File.Exists(filePath))
            return null;

        var viewPath = string.IsNullOrEmpty(relativeTemplatesPath)
            ? template
            : $"{relativeTemplatesPath}/{template}";
        return $"~/{viewPath}.cshtml";
    }

    private static bool IsLayout(string template) =>
        Path.GetFileName(template).Equals("Layout", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(template).StartsWith('_');

    private static Type GetModelType(IRazorViewEngine engine, string path)
    {
        var result = engine.GetView(null, path, isMainPage: true);
        if (result.View is not RazorView razorView)
            throw new InvalidOperationException($"Unable to find email template '{path}'.");

        for (var type = razorView.RazorPage.GetType().BaseType; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(RazorPage<>))
                return type.GetGenericArguments()[0];
        }

        return typeof(object);
    }

    private static object CreateDefaultModel(Type modelType)
    {
        if (modelType == typeof(object))
            return new Dictionary<string, object?>();

        var factory = modelType.GetMethod("CreateDefaultModel", BindingFlags.Public | BindingFlags.Static,
            binder: null, types: Type.EmptyTypes, modifiers: null);
        if (factory is null)
            throw new InvalidOperationException(
                $"Template model '{modelType.Name}' must expose a public static CreateDefaultModel() method.");

        var model = factory.Invoke(null, null);
        if (model is null || !modelType.IsInstanceOfType(model))
            throw new InvalidOperationException(
                $"Template model '{modelType.Name}' returned an invalid value from CreateDefaultModel().");

        return model;
    }

    private static async Task<IResult> RenderAsync(KitchenRenderRequest request, string templatesDirectory,
        string relativeTemplatesPath, string kitchenRoot, IRazorViewEngine engine,
        IRazorViewRendererService renderer, CancellationToken ct)
    {
        var path = GetTemplatePath(templatesDirectory, relativeTemplatesPath, request.Template);
        if (path is null)
            return Results.NotFound();
        if (request.Model.ValueKind != JsonValueKind.Object)
            return Results.BadRequest("Model JSON must contain an object.");

        try
        {
            var html = await RenderTemplateAsync(path, request.Model, engine, renderer, ct);
            var logoUrl = GetLogoUrl(kitchenRoot);
            html = html.Replace(EmailLogo.Source, logoUrl, StringComparison.Ordinal);
            return Results.Content(html, "text/html; charset=utf-8");
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<IResult> SendAsync(KitchenSendRequest request, string templatesDirectory,
        string relativeTemplatesPath, HttpContext context,
        IAntiforgery antiforgery, IRazorViewEngine engine, IRazorViewRendererService renderer,
        ILoggerFactory loggerFactory, CancellationToken ct)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest("Refresh Email Kitchen and try again.");

        var sender = context.RequestServices.GetService<IEmailSender>();
        if (sender is null)
            return Results.Problem("IEmailSender has not been implemented, so a test email cannot be sent.",
                statusCode: StatusCodes.Status501NotImplemented);

        var path = GetTemplatePath(templatesDirectory, relativeTemplatesPath, request.Template);
        if (path is null)
            return Results.NotFound();
        if (request.Model.ValueKind != JsonValueKind.Object)
            return Results.BadRequest("Model JSON must contain an object.");
        if (string.IsNullOrWhiteSpace(request.To) || !MailAddress.TryCreate(request.To, out var recipient))
            return Results.BadRequest("Enter a valid recipient.");
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Contains('\r') ||
            request.Subject.Contains('\n'))
            return Results.BadRequest("Enter a valid subject.");

        try
        {
            var html = await RenderTemplateAsync(path, request.Model, engine, renderer, ct);
            var logoDataUri = await GetLogoDataUriAsync(ct);
            html = html.Replace(EmailLogo.Source, logoDataUri, StringComparison.Ordinal);
            await sender.SendHtmlAsync(recipient.Address, request.Subject.Trim(), html, ct);
            return Results.NoContent();
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory.CreateLogger(nameof(EmailKitchenEndpoints)).LogError(ex, "Email Kitchen send failed.");
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<string> GetLogoDataUriAsync(CancellationToken ct)
    {
        await using var stream = EmailLogo.Open();
        using var content = new MemoryStream();
        await stream.CopyToAsync(content, ct);
        return $"data:image/png;base64,{Convert.ToBase64String(content.ToArray())}";
    }

    private static async Task<string> RenderTemplateAsync(string path, JsonElement model, IRazorViewEngine engine,
        IRazorViewRendererService renderer, CancellationToken ct)
    {
        var viewModel = model.Deserialize(GetModelType(engine, path), ModelOptions)
                        ?? throw new JsonException("Model JSON must contain an object.");
        return await renderer.RenderViewToStringAsync(path, (dynamic)viewModel, ct);
    }

    private sealed record KitchenRenderRequest(string Template, JsonElement Model);

    private sealed record KitchenSendRequest(string Template, JsonElement Model, string? To, string? Subject);
}