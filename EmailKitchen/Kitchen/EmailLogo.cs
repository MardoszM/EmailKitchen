namespace EmailKitchen.Kitchen;

/// <summary>
/// Provides the embedded default logo and its content ID reference for email templates.
/// </summary>
public static class EmailLogo
{
    /// <summary>
    /// The MIME content ID used to identify the default Email Kitchen logo.
    /// </summary>
    public const string ContentId = "email-kitchen-logo@emailkitchen";
    /// <summary>
    /// The <c>cid:</c> image source that Kitchen replaces with a preview URL or inline image data.
    /// </summary>
    public const string Source = "cid:" + ContentId;
    private const string ResourceName = "EmailKitchen.email-kitchen-logo.png";

    /// <summary>
    /// Opens a new stream containing the embedded default logo in PNG format.
    /// </summary>
    /// <returns>A readable stream containing the logo. The caller must dispose the stream.</returns>
    /// <exception cref="InvalidOperationException">The embedded logo resource is missing.</exception>
    public static Stream Open() =>
        typeof(EmailLogo).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException("The default Email Kitchen logo is missing from embedded resources.");
}
