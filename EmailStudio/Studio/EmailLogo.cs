namespace EmailStudio.Studio;

public static class EmailLogo
{
    public const string ContentId = "email-studio-logo@emailstudio";
    public const string Source = "cid:" + ContentId;
    private const string ResourceName = "EmailStudio.email-studio-logo.png";

    public static Stream Open() =>
        typeof(EmailLogo).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException("The default Email Studio logo is missing from embedded resources.");
}
