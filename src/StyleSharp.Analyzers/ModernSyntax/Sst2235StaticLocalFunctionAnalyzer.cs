// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports local functions that can be marked static because they do not capture outer state (SST2235).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2235StaticLocalFunctionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 8 language-version value.</summary>
    private const int CSharp8 = 800;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.MakeLocalFunctionStatic);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeLocalFunction, SyntaxKind.LocalFunctionStatement);
    }

    /// <summary>Reports capture-free local functions that are not already static.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context)
    {
        var localFunction = (LocalFunctionStatementSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(localFunction, CSharp8)
            || ModifierListHelper.Contains(localFunction.Modifiers, SyntaxKind.StaticKeyword)
            || !IsCaptureFree(localFunction, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ModernSyntaxRules.MakeLocalFunctionStatic,
            localFunction.Identifier.GetLocation(),
            localFunction.Identifier.ValueText));
    }

    /// <summary>Returns whether the local function body does not reference outer locals, parameters, or instance members.</summary>
    /// <param name="localFunction">The local function.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when adding static should compile.</returns>
    private static bool IsCaptureFree(
        LocalFunctionStatementSyntax localFunction,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        SyntaxNode? body = localFunction.Body ?? (SyntaxNode?)localFunction.ExpressionBody?.Expression;
        if (body is null)
        {
            return false;
        }

        foreach (var node in body.DescendantNodesAndSelf(static node => node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax))
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (node)
            {
                case ThisExpressionSyntax:
                case BaseExpressionSyntax:
                    return false;
                case IdentifierNameSyntax identifier when IsCapturedReference(identifier, localFunction, model, cancellationToken):
                    return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an identifier would be illegal inside a static local function.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <param name="localFunction">The local function.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the identifier captures outer state.</returns>
    private static bool IsCapturedReference(
        IdentifierNameSyntax identifier,
        LocalFunctionStatementSyntax localFunction,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(identifier, cancellationToken).Symbol;
        if (symbol is ILocalSymbol or IParameterSymbol)
        {
            return !IsDeclaredInside(symbol, localFunction);
        }

        return symbol switch
        {
            IFieldSymbol or IPropertySymbol or IEventSymbol => true,
            IMethodSymbol { IsStatic: false, MethodKind: MethodKind.Ordinary } => true,
            _ => false
        };
    }

    /// <summary>Returns whether a symbol is declared within the local function span.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="localFunction">The local function.</param>
    /// <returns><see langword="true"/> when all source declarations belong to the local function.</returns>
    private static bool IsDeclaredInside(ISymbol symbol, LocalFunctionStatementSyntax localFunction)
    {
        var references = symbol.DeclaringSyntaxReferences;
        if (references.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < references.Length; i++)
        {
            if (!localFunction.Span.Contains(references[i].Span))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether the syntax tree uses at least the supplied language version.</summary>
    /// <param name="node">The syntax node.</param>
    /// <param name="version">The numeric language version.</param>
    /// <returns><see langword="true"/> when the feature is available.</returns>
    private static bool IsLanguageVersionAtLeast(SyntaxNode node, int version)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= version;
}
