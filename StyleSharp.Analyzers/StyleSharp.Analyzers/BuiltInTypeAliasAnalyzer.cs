// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a framework type name used where its built-in C# keyword alias would read better
/// (SST1121) — <c>Int32</c> or <c>System.Int32</c> instead of <c>int</c>. Disabled by default;
/// Roslynator's RCS1013 covers the same ground faster.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BuiltInTypeAliasAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.UseBuiltInTypeAlias);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
        context.RegisterSyntaxNodeAction(AnalyzeQualified, SyntaxKind.QualifiedName);
    }

    /// <summary>Reports a bare framework type name (<c>Int32</c>).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
    {
        var identifier = (IdentifierNameSyntax)context.Node;
        if (!BuiltInTypeAliases.IsAliasedName(identifier.Identifier.ValueText))
        {
            return;
        }

        // A qualified name ('System.Int32') and member access ('x.Int32') own the name; handle those elsewhere.
        if (identifier.Parent is QualifiedNameSyntax qualified && qualified.Right == identifier)
        {
            return;
        }

        if (identifier.Parent is MemberAccessExpressionSyntax member && member.Name == identifier)
        {
            return;
        }

        Report(context, identifier);
    }

    /// <summary>Reports a qualified framework type name (<c>System.Int32</c>).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeQualified(SyntaxNodeAnalysisContext context)
    {
        var qualified = (QualifiedNameSyntax)context.Node;

        // A nested qualified name is the left part of a larger name; only the outermost is the full type.
        if (qualified.Parent is QualifiedNameSyntax)
        {
            return;
        }

        if (qualified.Right is not IdentifierNameSyntax right || !BuiltInTypeAliases.IsAliasedName(right.Identifier.ValueText))
        {
            return;
        }

        Report(context, qualified);
    }

    /// <summary>Reports the type node when it binds to a special type that has a keyword alias.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="node">The candidate type node.</param>
    private static void Report(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        if (context.SemanticModel.GetSymbolInfo(node, context.CancellationToken).Symbol is not INamedTypeSymbol type
            || BuiltInTypeAliases.Keyword(type.SpecialType) is not { } keyword)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseBuiltInTypeAlias, node.GetLocation(), keyword, node.ToString()));
    }
}
