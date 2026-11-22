using EmailStudio.Templates.Shared;

namespace EmailStudio.Templates;

/// <summary>
/// Supplies the content and default preview data for the example test email.
/// </summary>
public sealed class TestEmailModel : LayoutModel
{
    /// <summary>
    /// Gets or sets the recipient's display name used in the greeting.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets an optional message override. When null, the selected language's message translation is used.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets an optional preview text override. When null, the selected language's preheader translation is used.
    /// </summary>
    public string? Preheader { get; set; }

    /// <summary>
    /// Creates the initial example data displayed in Email Studio's model editor.
    /// </summary>
    /// <returns>A new test email model with English selected and sample branding and recipient values.</returns>
    public static TestEmailModel CreateDefaultModel() => new()
    {
        Language = SupportedLanguages.eng,
        BrandName = "Your application",
        Name = "Alex Morgan"
    };
}
