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
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.UseNameofForSymbolName);

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
        var literal = (LiteralExpressionSyntax)context.Node;
        if (literal.Token.Value is not string { Length: > 0 } name
            || literal.Parent is not ArgumentSyntax argument
            || context.SemanticModel.GetOperation(argument, context.CancellationToken) is not IArgumentOperation { Parameter: { } parameter }
            || !IsNameShapedParameter(parameter.Name)
            || !HasVisibleSymbolNamed(context.SemanticModel, literal.SpanStart, name))
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
    /// <param name="position">The source position.</param>
    /// <param name="name">The symbol name.</param>
    /// <returns><see langword="true"/> when <c>nameof(name)</c> can bind.</returns>
    private static bool HasVisibleSymbolNamed(SemanticModel model, int position, string name)
    {
        var symbols = model.LookupSymbols(position, name: name);
        for (var i = 0; i < symbols.Length; i++)
        {
            if (symbols[i].Kind != SymbolKind.Namespace)
            {
                return true;
            }
        }

        return false;
    }
}
