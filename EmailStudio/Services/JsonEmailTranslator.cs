using System.Text.Json;
using EmailStudio.Studio;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace EmailStudio.Services;

/// <summary>
/// Reads email translations from JSON files in the configured template directory's <c>Translations</c> folder.
/// </summary>
/// <param name="env">The host environment providing the application's content root and environment name.</param>
/// <remarks>
/// Translation files are read on each lookup in Development and cached by language in other environments.
/// </remarks>
public sealed class JsonEmailTranslator(IWebHostEnvironment env) : IEmailTranslator
{
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _cache = new();
    private readonly string _translationsDirectory = Path.Combine(env.ContentRootPath,
        EmailStudioEndpoints.GetConfiguredTemplatesPath(), "Translations");

    /// <inheritdoc/>
    public string T(string key, string language, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(language) || Path.GetFileName(language) != language)
            throw new ArgumentException("Provide a translation file name without directories or an extension.", nameof(language));

        var translations = env.IsDevelopment() ? Load(language) : _cache.GetOrAdd(language, Load);
        if (!translations.TryGetValue(key, out var value))
            value = key;

        return args.Length == 0 ? value : string.Format(value, args);
    }

    private Dictionary<string, string> Load(string language)
    {
        var path = Path.Combine(_translationsDirectory, $"{language}.json");
        if (!File.Exists(path))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
               ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
