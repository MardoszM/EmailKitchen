using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EmailKitchen.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DefaultModelFactoryAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "EMAILKITCHEN001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Template model must provide a default factory",
        "Template model '{0}' must expose a public static CreateDefaultModel() method returning its own type",
        "EmailKitchen",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Email Kitchen uses CreateDefaultModel() to create the initial model JSON for a template.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || !HasMarkedBaseType(type) || HasFactory(type))
            return;

        var declaration = type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .FirstOrDefault();
        if (declaration is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, declaration.Identifier.GetLocation(), type.Name));
    }

    private static bool HasMarkedBaseType(INamedTypeSymbol type)
    {
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "EmailKitchen.EmailKitchenTemplateBaseAttribute"))
                return true;
        }

        return false;
    }

    private static bool HasFactory(INamedTypeSymbol type) => type.GetMembers("CreateDefaultModel")
        .OfType<IMethodSymbol>()
        .Any(method => method.MethodKind == MethodKind.Ordinary && method.IsStatic &&
                       method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 0 &&
                       SymbolEqualityComparer.Default.Equals(method.ReturnType, type));
}
