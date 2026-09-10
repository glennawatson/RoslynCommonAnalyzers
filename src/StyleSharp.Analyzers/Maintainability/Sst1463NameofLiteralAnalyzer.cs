// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Operations;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports string literals that are passed to name-shaped parameters and match an in-scope symbol.
/// The semantic work is gated behind two cheap syntax checks: the literal must be a normal string,
/// and the bound argument's parameter name must contain "name". This keeps arbitrary strings out
/// of the lookup path and avoids turning this into a broad string-literal analyzer.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1463NameofLiteralAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(MaintainabilityRules.UseNameofForSymbolName);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeLiteral, SyntaxKind.StringLiteralExpression);
    }

    /// <summary>Reports a name-shaped string literal that can use <c>nameof</c>.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp6 })
        {
            return;
        }

        var literal = (LiteralExpressionSyntax)context.Node;
        if (literal.Token.Value is not string { Length: > 0 } name
            || literal.Parent is not ArgumentSyntax argument
            || context.SemanticModel.GetOperation(argument, context.CancellationToken) is not IArgumentOperation { Parameter: { } parameter }
            || !IsNameShapedParameter(parameter.Name)
            || !HasVisibleSymbolNamed(context.SemanticModel, literal, name, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.UseNameofForSymbolName,
            literal.GetLocation(),
            name));
    }

    /// <summary>Returns whether a parameter is likely to receive a symbol name.</summary>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns><see langword="true"/> when the name includes "name".</returns>
    private static bool IsNameShapedParameter(string parameterName)
        => parameterName.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>Returns whether a non-namespace symbol with the supplied name is visible at a source position.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="literal">The string literal standing where the name would go.</param>
    /// <param name="name">The symbol name.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when <c>nameof(name)</c> can bind.</returns>
    /// <remarks>
    /// A generic type does not count, because its bare name is not a name the language will take: where the
    /// only <c>TimeInterval</c> in scope is <c>TimeInterval&lt;T&gt;</c>, <c>nameof(TimeInterval)</c> is
    /// CS0305 and not a fix at all. The rule suggests <c>nameof</c> only where the name written in the
    /// string is a name that compiles as written.
    /// </remarks>
    private static bool HasVisibleSymbolNamed(SemanticModel model, LiteralExpressionSyntax literal, string name, CancellationToken cancellationToken)
    {
        var symbols = model.LookupSymbols(literal.SpanStart, name: name);
        for (var i = 0; i < symbols.Length; i++)
        {
            if (symbols[i] is INamedTypeSymbol { Arity: > 0 } || symbols[i].Kind == SymbolKind.Namespace)
            {
                continue;
            }

            if (!IsDeclaredAroundTheLiteral(symbols[i], literal, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a local's own declaration is what encloses the literal.</summary>
    /// <param name="symbol">The symbol the name resolves to.</param>
    /// <param name="literal">The string literal standing where the name would go.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when naming the symbol here would name it before it exists.</returns>
    /// <remarks>
    /// A local is in scope across its whole block, so the lookup finds one that is not declared yet where
    /// the literal stands — <c>var value = Find(nameof(value))</c>, or <c>Find(nameof(x)) is not { } x</c>.
    /// Naming it there is CS0841, so the local has to be fully declared ahead of the literal to count.
    /// </remarks>
    private static bool IsDeclaredAroundTheLiteral(ISymbol symbol, LiteralExpressionSyntax literal, CancellationToken cancellationToken)
    {
        if (symbol is not ILocalSymbol)
        {
            return false;
        }

        var references = symbol.DeclaringSyntaxReferences;
        for (var i = 0; i < references.Length; i++)
        {
            if (references[i].SyntaxTree == literal.SyntaxTree
                && references[i].GetSyntax(cancellationToken).Span.End > literal.SpanStart)
            {
                return true;
            }
        }

        return false;
    }
}
