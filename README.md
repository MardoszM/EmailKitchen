# EmailStudio

EmailStudio provides a development email studio for ASP.NET Core applications. It renders the host project's `.cshtml` templates and can optionally send a test message through the host's email sender.

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

app.MapEmailStudio(enabled: app.Environment.IsDevelopment());
```

The `enabled` flag controls whether the studio endpoints are mapped. The studio is available at `/email-studio` when enabled.

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
