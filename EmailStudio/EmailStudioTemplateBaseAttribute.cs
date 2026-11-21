namespace EmailStudio;

/// <summary>
/// Marks a shared template model as the base for concrete Email Studio template models.
/// Concrete derived models must expose a public static CreateDefaultModel() factory.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EmailStudioTemplateBaseAttribute : Attribute
{
}
