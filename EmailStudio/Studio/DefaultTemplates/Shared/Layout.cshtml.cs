namespace EmailStudio.Templates.Shared;

/// <summary>
/// Provides the branding and footer values shared by the example email templates.
/// </summary>
[EmailStudioTemplateBase]
public class LayoutModel
{
    /// <summary>
    /// Gets or sets the application or brand name displayed in the email header.
    /// </summary>
    public string? BrandName { get; set; }

    /// <summary>
    /// Gets or sets the explanatory text displayed in the email footer.
    /// </summary>
    public string? FooterText { get; set; }
}