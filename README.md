# EmailKitchen

EmailKitchen is an email template editor for ASP.NET Core applications. It renders the host project's `.cshtml` templates and can optionally send a test message through the host's email sender.

## Install

Add the package to the ASP.NET Core application:

```xml
<PackageReference Include="EmailKitchen" Version="1.0.0" />
```

## Configure template location

Set the template folder in the consuming application's `.csproj`:

```xml
<PropertyGroup>
  <EmailKitchenTemplatesPath>./EmailKitchen/Templates</EmailKitchenTemplatesPath>
</PropertyGroup>
```

The path is relative to the application project directory. On the first build, the NuGet package copies any missing starter files there without overwriting existing templates. The `.cshtml` and `.cshtml.cs` files are included in the consuming project and can be edited and committed normally.

If `EmailKitchenTemplatesPath` is omitted, EmailKitchen uses `./EmailKitchen/Templates`. Missing starter files are copied to that location.

## Starter email translations

The starter files include `SupportedLanguages.cs` with English (`eng`) and `Translations/eng.json`.
The shared layout model exposes `Language`; `TestEmailModel.CreateDefaultModel()` selects `SupportedLanguages.eng`.
Both the layout and the test email read their text through `IEmailTranslator`, which `AddEmailKitchen()` registers automatically.

Choose the language through the model, including in EmailKitchen's Model JSON editor:

```json
{
  "language": "eng",
  "name": "Alex Morgan",
  "brandName": "Your application"
}
```

To add a language, extend the copied enum and add a JSON file with the same name in `Translations`,
for example `pl` and `Translations/pl.json`. Keep the translation keys and set `layout.language`
to the HTML language tag, such as `pl`. Numbered placeholders like `{0}` are filled by the translator.
`Message`, `Preheader`, and `FooterText` remain optional model overrides; leave them null to use translations.

Translation JSON files are copied to build and publish output along with the templates.
In Development they are reloaded on each render, and edits update the preview's revision.
Other environments cache translations until the application restarts. Missing files or keys display the key itself.

All starter files are copied only when the configured template folder contains no Razor templates.
Existing templates and their translations are preserved on subsequent builds and package updates.
Hosts with their own translation service can register `IEmailTranslator` before calling `AddEmailKitchen()`
or keep using their existing localization in their own templates.

## Map EmailKitchen

```csharp
using EmailKitchen.Kitchen;

if (app.Environment.IsDevelopment())
{
    app.MapEmailKitchen();
}
```

EmailKitchen is available at `/email-kitchen` when its endpoints are mapped.

## Preview email clients

Choose an email app above the preview to explore a local simulation of its rendering rules.
The browser original, client selection, compatibility notes, and light/dark preview are available
in both EmailKitchen and the separate Live Preview window. Changing the client uses the last rendered
HTML and does not send another render request or alter the HTML sent by `IEmailSender`.

Client previews are approximations, not screenshots from the actual email apps. In particular,
Outlook Classic's Word rendering engine is not running in the preview. Compatibility notes can
help identify unsupported CSS but do not guarantee an identical result in a recipient's inbox.

The pinned MIT-licensed Emailens engine runs in a browser worker, without an account, API key,
hosted service, or per-run quota. The bundled build removes its HTML-size and warning-location
caps; large emails still depend on the browser's available CPU and memory. Remote images and
fonts in the email may require network access. Third-party license notices are included in the
NuGet package and available at `/email-kitchen/assets/third-party-notices.txt`.

The prebuilt browser assets are checked in, so NuGet consumers need no Node.js installation.
To rebuild them when developing EmailKitchen, run `npm ci --ignore-scripts` and `npm run build`
in `tools/client-preview`. The build pins its dependencies and verifies the two local limit changes.

## Authorize production access

`MapEmailKitchen()` returns a `RouteGroupBuilder`. Apply `RequireAuthorization(...)` to the returned group to protect every EmailKitchen endpoint, including the interface, assets, models, previews, rendering, and test email sending.

The host application provides authentication, policies, and authorization handlers. With authentication already configured, for example using cookies or OpenID Connect, register EmailKitchen and a policy:

```csharp
using EmailKitchen.Services;

builder.Services.AddEmailKitchen();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EmailKitchenAccess", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));
});
```

After building the application, enable its authentication and authorization middleware and map EmailKitchen:

```csharp
using EmailKitchen.Kitchen;

app.UseAuthentication();
app.UseAuthorization();

app.MapEmailKitchen().RequireAuthorization("EmailKitchenAccess");
```

The host application's authorization policy controls access to EmailKitchen, including test email sending. Sending test messages requires a valid antiforgery token and a registered `IEmailSender`.

## Default model for a template

Each selectable template should expose a public static `CreateDefaultModel()` method on the type used by its `@model` directive. Email Kitchen calls this method to populate the Model JSON editor. The bundled Roslyn analyzer reports a build error and offers a quick fix when a concrete class derived from the shared model marked with `[EmailKitchenTemplateBase]` is missing this factory. The bundled `LayoutModel` is already marked. If you use your own shared base model, add the attribute to that class once:

```csharp
using EmailKitchen;

[EmailKitchenTemplateBase]
public class LayoutModel { }
```

Keep sample values in this factory rather than in property initializers:

```csharp
public sealed class WelcomeEmailModel
{
    public string? Name { get; set; }

    public static WelcomeEmailModel CreateDefaultModel() => new()
    {
        Name = "Alex Morgan"
    };
}
```
