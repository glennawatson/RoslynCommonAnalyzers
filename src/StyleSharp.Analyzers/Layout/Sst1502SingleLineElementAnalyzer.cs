// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an element whose non-empty body is collapsed onto a single line (SST1502):
/// a method, constructor, destructor, operator, local function, or a type declaration
/// that holds members. Empty bodies and expression-bodied members are exempt; accessor
/// bodies are governed by SST1504.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1502SingleLineElementAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The element kinds the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.MethodDeclaration,
        SyntaxKind.ConstructorDeclaration,
        SyntaxKind.DestructorDeclaration,
        SyntaxKind.OperatorDeclaration,
        SyntaxKind.ConversionOperatorDeclaration,
        SyntaxKind.LocalFunctionStatement,
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.EnumDeclaration);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.ElementOnOwnLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Resolves the element's brace pair and member count, then reports a single-line body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (!TryGetBody(context.Node, out var open, out var close, out var hasContent) || !hasContent)
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        if (LayoutHelpers.StartLine(text, open) != LayoutHelpers.StartLine(text, close))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.ElementOnOwnLine, open.GetLocation(), DescribeElement(context.Node)));
    }

    /// <summary>Extracts the brace-delimited body of an element and whether it has content.</summary>
    /// <param name="node">The element node.</param>
    /// <param name="open">The opening brace, when found.</param>
    /// <param name="close">The closing brace, when found.</param>
    /// <param name="hasContent">Whether the body holds at least one statement or member.</param>
    /// <returns><see langword="true"/> when the element has a brace-delimited body.</returns>
    private static bool TryGetBody(SyntaxNode node, out SyntaxToken open, out SyntaxToken close, out bool hasContent)
    {
        var body = node switch
        {
            BaseMethodDeclarationSyntax method => method.Body,
            LocalFunctionStatementSyntax local => local.Body,
            _ => null
        };

        if (body is not null)
        {
            open = body.OpenBraceToken;
            close = body.CloseBraceToken;
            hasContent = body.Statements.Count > 0;
            return true;
        }

        switch (node)
        {
            case TypeDeclarationSyntax type:
            {
                open = type.OpenBraceToken;
                close = type.CloseBraceToken;
                hasContent = type.Members.Count > 0;
                return !open.IsKind(SyntaxKind.None) && !open.IsMissing;
            }

            case EnumDeclarationSyntax @enum:
            {
                open = @enum.OpenBraceToken;
                close = @enum.CloseBraceToken;
                hasContent = @enum.Members.Count > 0;
                return !open.IsKind(SyntaxKind.None) && !open.IsMissing;
            }

            default:
            {
                open = default;
                close = default;
                hasContent = false;
                return false;
            }
        }
    }

    /// <summary>Returns a short noun describing the element for the diagnostic message.</summary>
    /// <param name="node">The element node.</param>
    /// <returns>A human-readable element description.</returns>
    private static string DescribeElement(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        DestructorDeclarationSyntax destructor => destructor.Identifier.ValueText,
        LocalFunctionStatementSyntax local => local.Identifier.ValueText,
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        _ => "the element"
    };
}
