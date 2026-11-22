namespace EmailStudio.Studio;

/// <summary>
/// Provides the embedded default logo and its content ID reference for email templates.
/// </summary>
public static class EmailLogo
{
    /// <summary>
    /// The MIME content ID used to identify the default Email Studio logo.
    /// </summary>
    public const string ContentId = "email-studio-logo@emailstudio";
    /// <summary>
    /// The <c>cid:</c> image source that Studio replaces with a preview URL or inline image data.
    /// </summary>
    public const string Source = "cid:" + ContentId;
    private const string ResourceName = "EmailStudio.email-studio-logo.png";

    /// <summary>
    /// Opens a new stream containing the embedded default logo in PNG format.
    /// </summary>
    /// <returns>A readable stream containing the logo. The caller must dispose the stream.</returns>
    /// <exception cref="InvalidOperationException">The embedded logo resource is missing.</exception>
    public static Stream Open() =>
        typeof(EmailLogo).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException("The default Email Studio logo is missing from embedded resources.");
}
