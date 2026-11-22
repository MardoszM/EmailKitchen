namespace EmailKitchen.Templates.Shared;

/// <summary>
/// Provides the language, branding, and optional footer override shared by the example email templates.
/// </summary>
[EmailKitchenTemplateBase]
public class LayoutModel
{
    /// <summary>
    /// Gets or sets the language used to load translations for the email and its layout.
    /// </summary>
    public SupportedLanguages Language { get; set; }

    /// <summary>
    /// Gets or sets the application or brand name displayed in the email header.
    /// </summary>
    public string? BrandName { get; set; }

    /// <summary>
    /// Gets or sets an optional footer override. When null, the selected language's footer translation is used.
    /// </summary>
    public string? FooterText { get; set; }
}
