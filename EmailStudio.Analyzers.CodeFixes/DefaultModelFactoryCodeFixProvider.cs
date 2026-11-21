using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace EmailStudio.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DefaultModelFactoryCodeFixProvider)), Shared]
public sealed class DefaultModelFactoryCodeFixProvider : CodeFixProvider
{
    private const string DiagnosticId = "EMAILSTUDIO001";
    private const string Title = "Generate CreateDefaultModel()";

    public override ImmutableArray<string> FixableDiagnosticIds => [DiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var declaration = root?.FindNode(context.Span)
            .FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (declaration is null || declaration.Members.OfType<MethodDeclarationSyntax>()
                .Any(method => method.Identifier.ValueText == "CreateDefaultModel"))
            return;

        context.RegisterCodeFix(
            CodeAction.Create(Title, cancellationToken => AddFactoryAsync(context.Document, declaration,
                cancellationToken), Title),
            context.Diagnostics);
    }

    private static async Task<Document> AddFactoryAsync(Document document, TypeDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        TypeSyntax returnType = SyntaxFactory.IdentifierName(declaration.Identifier);
        if (declaration.TypeParameterList is { Parameters.Count: > 0 } typeParameters)
        {
            returnType = SyntaxFactory.GenericName(declaration.Identifier,
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList<TypeSyntax>(typeParameters.Parameters.Select(parameter =>
                        SyntaxFactory.IdentifierName(parameter.Identifier)))));
        }

        var method = SyntaxFactory.ParseMemberDeclaration($$"""
            public static {{returnType}} CreateDefaultModel() => new()
            {
                // TODO: Set representative default values for this template.
            };
            """)!
            .WithAdditionalAnnotations(Formatter.Annotation);

        var updatedDeclaration = declaration.AddMembers(method);
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root!.ReplaceNode(declaration, updatedDeclaration));
    }
}
