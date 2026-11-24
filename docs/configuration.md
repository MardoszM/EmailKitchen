# Configure EmailKitchen

Use these options after [installing EmailKitchen](../README.md#installation).

## Choose where your templates live

EmailKitchen uses `EmailKitchen/Templates` by default. To use a different folder, set `EmailKitchenTemplatesPath` in your web project's `.csproj`. The path must be relative to and inside the project directory.

```xml
<PropertyGroup>
  <EmailKitchenTemplatesPath>Features/Email/Templates</EmailKitchenTemplatesPath>
</PropertyGroup>
```

When the configured folder has no Razor views, the build scaffolds `TestEmail.cshtml`, its C# model, a shared layout, and English translations. Once a Razor view exists, subsequent builds keep your files. Nested folders work out of the box. `Layout.cshtml` and views whose names start with `_` are kept out of the template picker.

## Add a template with sample data

For a typed view, add a public static, parameterless `CreateDefaultModel()` to its model type. This is the data EmailKitchen displays when you select the template. Here is a welcome email that uses the starter layout.

**`WelcomeEmail.cshtml.cs`**

```csharp
using EmailKitchen.Templates.Shared;

namespace EmailKitchen.Templates;

public sealed class WelcomeEmailModel : LayoutModel
{
    public string Name { get; init; } = "";

    public static WelcomeEmailModel CreateDefaultModel() => new()
    {
        Language = SupportedLanguages.eng,
        BrandName = "Northstar",
        Name = "Alex Morgan"
    };
}
```

**`WelcomeEmail.cshtml`**

```razor
@model EmailKitchen.Templates.WelcomeEmailModel
@{
    Layout = "Shared/Layout.cshtml";
    ViewData["EmailKitchenLayout"] = Model;
    ViewData["Title"] = "Welcome to Northstar";
}

<h1 style="margin:0 0 16px;font:700 28px Arial,sans-serif;">Hello @Model.Name!</h1>
<p style="margin:0;font:16px/24px Arial,sans-serif;">Your workspace is ready.</p>
```

Save both files next to `TestEmail.cshtml`.

For your own shared model base, add `[EmailKitchenTemplateBase]` from the `EmailKitchen` namespace. The bundled Roslyn analyzer reports `EMAILKITCHEN001` if a concrete descendant lacks a factory returning its own type. IDEs that load Roslyn code fixes can generate the method. The starter `LayoutModel` already enables this check.

## Add a language

The starter resolves translations through `IEmailTranslator`. To add Polish, add `pl` to `SupportedLanguages` and copy `Translations/eng.json` to `Translations/pl.json`. Translate the values, preserve the keys and numbered placeholders, and change the `layout.language` value to `pl`. In **Ingredients (JSON)**, set `"language": "pl"` and render.

The starter's `Message`, `Preheader`, and `FooterText` fields override translated text when supplied. In Development the JSON files are read on lookup. Other environments cache translations by language until restart. Missing entries display the key. For an existing localization service, register your implementation of `IEmailTranslator` before `AddEmailKitchen()` or inject your service directly into the views.

## Enable test email delivery

Register your delivery adapter as `EmailKitchen.Services.IEmailSender` before `builder.Build()`. In this example, `IApplicationMailer` is your application's registered mail service.

```csharp
builder.Services.AddScoped<IEmailSender, AppEmailSender>();
```

```csharp
using EmailKitchen.Services;

public sealed class AppEmailSender(IApplicationMailer mailer) : IEmailSender
{
    public Task SendHtmlAsync(string to, string subject, string html, CancellationToken ct = default)
        => mailer.SendHtmlAsync(to, subject, html, ct);
}
```

The send endpoint requires a valid antiforgery token, which the workspace supplies. The adapter controls delivery and credentials.

## Render Razor email to HTML from C#

Inject `IRazorViewRendererService` or resolve it from a scope in your background worker. Pass the app-relative path of the view and the model for the message.

```csharp
var html = await renderer.RenderViewToStringAsync(
    "~/EmailKitchen/Templates/WelcomeEmail.cshtml", model, cancellationToken);
```

You can register a custom renderer before `AddEmailKitchen()`. If you send the starter layout's HTML directly, it can reference the bundled logo as a CID. Supply the inline image using `EmailLogo.Open()` and `EmailLogo.ContentId`, or use your own image URL in the layout.

## Enable access in a shared environment

Use the host application's authentication and apply an authorization policy to the entire EmailKitchen route group. For example, register a policy requiring the **Admin** role before building the app.

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EmailKitchenAccess", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));
});
```

Use the host's authentication and authorization middleware. Replace the development-only mapping with the protected route group.

```csharp
app.UseAuthentication();
app.UseAuthorization();

app.MapEmailKitchen().RequireAuthorization("EmailKitchenAccess");
```

The policy covers the workspace, assets, template and model data, previews, rendering, and sending. For a published app, include the Razor source files in the deployed content root so the workspace can discover them. Set this in the web project's `.csproj`.

```xml
<PropertyGroup>
  <CopyRazorGenerateFilesToPublishDirectory>true</CopyRazorGenerateFilesToPublishDirectory>
</PropertyGroup>
```

## Install from source

Pack a local release from the repository root.

```bash
dotnet pack EmailKitchen/EmailKitchen.csproj -c Release -p:PackageVersion=1.0.0-local.1 -o ./artifacts/packages
```

Then install it from your web project's directory.

```bash
dotnet package add EmailKitchen --version 1.0.0-local.1 --source /path/to/EmailKitchen/artifacts/packages
```

The package includes the prebuilt browser assets. To rebuild the Emailens bundle when contributing, run `npm ci --ignore-scripts` and `npm run build` from `tools/client-preview`.
