namespace EmailStudio;

/// <summary>
/// Marks a shared template model as the base for concrete Email Studio template models.
/// </summary>
/// <remarks>
/// Each concrete model derived from a marked base class must declare a public, static, parameterless
/// <c>CreateDefaultModel()</c> method returning its own type. Studio uses that factory to populate
/// the initial model JSON. The Email Studio analyzer checks this requirement at build time.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EmailStudioTemplateBaseAttribute : Attribute
{
}
