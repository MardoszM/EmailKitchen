using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Reflection;
using EmailStudio.Services;
using Microsoft.AspNetCore.Http;
using System.Text.Encodings.Web;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Razor;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;

namespace EmailStudio.Studio;

public static class EmailStudioEndpoints
{
    private const string Route = "/email-studio";
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private static readonly (string Resource, string RelativePath)[] DefaultTemplates =
    [
        ("EmailStudio.DefaultTemplates.Shared.Layout.cshtml", "Shared/Layout.cshtml"),
        ("EmailStudio.DefaultTemplates.Shared.Layout.cshtml.cs", "Shared/Layout.cshtml.cs"),
        ("EmailStudio.DefaultTemplates.TestEmail.cshtml", "TestEmail.cshtml"),
        ("EmailStudio.DefaultTemplates.TestEmail.cshtml.cs", "TestEmail.cshtml.cs")
    ];

    private static readonly JsonSerializerOptions ModelOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void MapEmailStudio(this WebApplication app, bool enabled = true, string? logoPath = null)
    {
        if (!enabled)
            return;

        var templatesPath = GetConfiguredTemplatesPath();
        var useProjectRoot = string.IsNullOrWhiteSpace(templatesPath) || templatesPath == ".";
        var (templatesDirectory, relativeTemplatesPath) = ResolveTemplatesPath(app.Environment.ContentRootPath,
            useProjectRoot ? "." : templatesPath!);
        CreateDefaultTemplatesIfMissing(templatesDirectory);
        var customLogoPath = ResolveLogoPath(app.Environment.ContentRootPath, logoPath);

        var studio = app.MapGroup(Route);
        studio.MapGet("", (HttpContext context, IAntiforgery antiforgery) =>
        {
            using var reader = new StreamReader(OpenAsset("studio.html"));
            var token = HtmlEncoder.Default.Encode(antiforgery.GetAndStoreTokens(context).RequestToken!);
            var html = ReplaceStudioRoot(reader.ReadToEnd(), GetStudioRoot(context), customLogoPath);
            return Results.Content(html.Replace("__ANTIFORGERY_TOKEN__", token),
                "text/html; charset=utf-8");
        });
        studio.MapGet("/assets/{asset}", (string asset, HttpContext context) => asset switch
        {
            "studio.css" => ServeAsset(asset, "text/css; charset=utf-8"),
            "studio.js" => ServeAsset(asset, "text/javascript; charset=utf-8", GetStudioRoot(context)),
            "email-studio-logo.png" => ServeLogo(context, customLogoPath),
            _ => Results.NotFound()
        });
        studio.MapGet("/templates", (IServiceProviderIsService services) =>
            Results.Ok(ReadCatalog(templatesDirectory, services.IsService(typeof(IEmailSender)))));
        studio.MapGet("/model/{**template}", (string template, IRazorViewEngine engine) =>
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
        studio.MapPost("/render", (StudioRenderRequest request, HttpContext context, IRazorViewEngine engine,
                IRazorViewRendererService renderer, CancellationToken ct) =>
            RenderAsync(request, templatesDirectory, relativeTemplatesPath, GetStudioRoot(context), customLogoPath,
                engine, renderer, ct));
        studio.MapPost("/send", (StudioSendRequest request, HttpContext context,
                IAntiforgery antiforgery, IRazorViewEngine engine, IRazorViewRendererService renderer,
                ILoggerFactory loggerFactory, CancellationToken ct) =>
            SendAsync(request, templatesDirectory, relativeTemplatesPath, customLogoPath, context, antiforgery,
                engine, renderer, loggerFactory, ct));
        studio.MapGet("/preview/{**template}", (string template, HttpContext context) =>
            GetTemplatePath(templatesDirectory, relativeTemplatesPath, template) is null
                ? Results.NotFound()
                : ServeAsset("preview.html", "text/html; charset=utf-8", GetStudioRoot(context), customLogoPath));
    }

    private static IResult ServeAsset(string name, string contentType) => Results.File(OpenAsset(name), contentType);

    private static IResult ServeAsset(string name, string contentType, string studioRoot,
        string? customLogoPath = null)
    {
        using var reader = new StreamReader(OpenAsset(name));
        return Results.Content(ReplaceStudioRoot(reader.ReadToEnd(), studioRoot, customLogoPath), contentType);
    }

    private static IResult ServeLogo(HttpContext context, string? customLogoPath)
    {
        context.Response.Headers.CacheControl = "no-store";

        if (customLogoPath is not null && ContentTypes.TryGetContentType(customLogoPath, out var contentType))
            return Results.File(customLogoPath, contentType);

        return Results.File(EmailLogo.Open(), "image/png");
    }

    private static string GetStudioRoot(HttpContext context) => $"{context.Request.PathBase}{Route}";

    private static string ReplaceStudioRoot(string content, string studioRoot, string? customLogoPath = null) =>
        content.Replace("__EMAIL_STUDIO_ROOT__", studioRoot, StringComparison.Ordinal)
            .Replace("__EMAIL_STUDIO_LOGO_URL__", GetLogoUrl(studioRoot, customLogoPath), StringComparison.Ordinal);

    private static string GetLogoUrl(string studioRoot, string? customLogoPath)
    {
        using var logo = customLogoPath is null ? EmailLogo.Open() : File.OpenRead(customLogoPath);
        var version = Convert.ToHexStringLower(SHA256.HashData(logo));
        return $"{studioRoot}/assets/email-studio-logo.png?v={version}";
    }

    private static Stream OpenAsset(string name) =>
        typeof(EmailStudioEndpoints).Assembly.GetManifestResourceStream($"EmailStudio.{name}")!;

    private static string? GetConfiguredTemplatesPath() =>
        Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "EmailStudioTemplatesPath")?.Value;

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

    private static string? ResolveLogoPath(string contentRoot, string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath))
            return null;

        var path = Path.GetFullPath(Path.IsPathRooted(logoPath)
            ? logoPath
            : Path.Combine(contentRoot, logoPath));
        if (!File.Exists(path))
            throw new FileNotFoundException("The configured Email Studio logo file was not found.", path);
        if (!ContentTypes.TryGetContentType(path, out var contentType) ||
            !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The configured Email Studio logo must be a supported image file.",
                nameof(logoPath));

        return path;
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
            using var source = typeof(EmailStudioEndpoints).Assembly.GetManifestResourceStream(resourceName)
                               ?? throw new InvalidOperationException(
                                   $"Default Email Studio template '{resourceName}' is missing.");
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

    private static async Task<IResult> RenderAsync(StudioRenderRequest request, string templatesDirectory,
        string relativeTemplatesPath, string studioRoot, string? customLogoPath, IRazorViewEngine engine,
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
            var logoUrl = GetLogoUrl(studioRoot, customLogoPath);
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

    private static async Task<IResult> SendAsync(StudioSendRequest request, string templatesDirectory,
        string relativeTemplatesPath, string? customLogoPath, HttpContext context,
        IAntiforgery antiforgery, IRazorViewEngine engine, IRazorViewRendererService renderer,
        ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null || !IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest("Refresh Email Studio and try again.");

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
            var logoDataUri = await GetLogoDataUriAsync(customLogoPath, ct);
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
            loggerFactory.CreateLogger(nameof(EmailStudioEndpoints)).LogError(ex, "Email Studio send failed.");
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<string> GetLogoDataUriAsync(string? customLogoPath, CancellationToken ct)
    {
        byte[] bytes;
        string contentType;
        if (customLogoPath is not null)
        {
            bytes = await File.ReadAllBytesAsync(customLogoPath, ct);
            ContentTypes.TryGetContentType(customLogoPath, out var resolvedContentType);
            contentType = resolvedContentType ?? "application/octet-stream";
        }
        else
        {
            await using var stream = EmailLogo.Open();
            using var content = new MemoryStream();
            await stream.CopyToAsync(content, ct);
            bytes = content.ToArray();
            contentType = "image/png";
        }

        return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
    }

    private static async Task<string> RenderTemplateAsync(string path, JsonElement model, IRazorViewEngine engine,
        IRazorViewRendererService renderer, CancellationToken ct)
    {
        var viewModel = model.Deserialize(GetModelType(engine, path), ModelOptions)
                        ?? throw new JsonException("Model JSON must contain an object.");
        return await renderer.RenderViewToStringAsync(path, (dynamic)viewModel, ct);
    }

    private sealed record StudioRenderRequest(string Template, JsonElement Model);

    private sealed record StudioSendRequest(string Template, JsonElement Model, string? To, string? Subject);
}
