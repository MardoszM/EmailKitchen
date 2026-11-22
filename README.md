# EmailStudio

EmailStudio provides an email template studio for ASP.NET Core applications. It renders the host project's `.cshtml` templates and can optionally send a test message through the host's email sender.

## Install

Add the package to the ASP.NET Core application:

```xml
<PackageReference Include="EmailStudio" Version="1.0.0" />
```

## Configure template location

Set the template folder in the consuming application's `.csproj`:

```xml
<PropertyGroup>
  <EmailStudioTemplatesPath>./EmailStudio/Templates</EmailStudioTemplatesPath>
</PropertyGroup>
```

The path is relative to the application project directory. On the first build, the NuGet package copies any missing starter files there without overwriting existing templates. The `.cshtml` and `.cshtml.cs` files are included in the consuming project and can be edited and committed normally.

If `EmailStudioTemplatesPath` is omitted, EmailStudio uses `./EmailStudio/Templates`. Missing starter files are copied to that location.

## Map the studio

```csharp
using EmailStudio.Studio;

if (app.Environment.IsDevelopment())
{
    app.MapEmailStudio();
}
```

The studio is available at `/email-studio` when its endpoints are mapped.

## Authorize production access

`MapEmailStudio()` returns a `RouteGroupBuilder`. Apply `RequireAuthorization(...)` to the returned group to protect every Studio endpoint, including the interface, assets, models, previews, rendering, and test email sending.

The host application provides authentication, policies, and authorization handlers. With authentication already configured, for example using cookies or OpenID Connect, register Studio and a policy:

```csharp
using EmailStudio.Services;

builder.Services.AddEmailStudio();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EmailStudioAccess", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));
});
```

After building the application, enable its authentication and authorization middleware and map Studio:

```csharp
using EmailStudio.Studio;

app.UseAuthentication();
app.UseAuthorization();

app.MapEmailStudio().RequireAuthorization("EmailStudioAccess");
```

The host application's authorization policy controls access to Studio, including test email sending. Sending test messages requires a valid antiforgery token and a registered `IEmailSender`.

## Default model for a template

Each selectable template should expose a public static `CreateDefaultModel()` method on the type used by its `@model` directive. Email Studio calls this method to populate the Model JSON editor. The bundled Roslyn analyzer reports a build error and offers a quick fix when a concrete class derived from the shared model marked with `[EmailStudioTemplateBase]` is missing this factory. The bundled `LayoutModel` is already marked. If you use your own shared base model, add the attribute to that class once:

```csharp
using EmailStudio;

[EmailStudioTemplateBase]
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
