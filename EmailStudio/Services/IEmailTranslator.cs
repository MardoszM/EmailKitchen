namespace EmailStudio.Services;

/// <summary>
/// Looks up and formats translated text used by email templates.
/// </summary>
public interface IEmailTranslator
{
    /// <summary>
    /// Gets a translation for the requested language and inserts any supplied format arguments.
    /// </summary>
    /// <param name="key">The translation key to look up.</param>
    /// <param name="language">The translation file name without its extension, for example <c>eng</c>.</param>
    /// <param name="args">Values to insert into numbered placeholders such as <c>{0}</c>.</param>
    /// <returns>The formatted translation, or the key when the file or translation is missing.</returns>
    /// <exception cref="ArgumentException">The language is empty or contains directory components.</exception>
    /// <exception cref="FormatException">The translation's placeholders do not match the supplied arguments.</exception>
    string T(string key, string language, params object[] args);
}
