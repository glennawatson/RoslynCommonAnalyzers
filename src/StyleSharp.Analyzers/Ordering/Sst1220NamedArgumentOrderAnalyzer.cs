// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a call whose arguments are all named but listed in a different order from the parameters
/// (SST1220). The rule only fires when every argument is named, so the compiler already binds each by
/// name and re-ordering them to declaration order is a pure readability change. The clean path is a
/// syntactic scan that bails as soon as a positional argument is seen, so the semantic model is consulted
/// only for the rare all-named call.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1220NamedArgumentOrderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The fewest arguments a reorder needs.</summary>
    private const int MinimumArguments = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(OrderingRules.NamedArgumentOrder);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.InvocationExpression,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    /// <summary>Returns the declaration position of the parameter with a name, or <c>-1</c> when absent.</summary>
    /// <param name="method">The bound method.</param>
    /// <param name="name">The named argument's name.</param>
    /// <returns>The zero-based parameter position, or <c>-1</c>.</returns>
    internal static int ParameterPosition(IMethodSymbol method, string name)
    {
        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (string.Equals(parameters[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Analyzes one call for out-of-order named arguments.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (ArgumentBinding.GetArgumentList(context.Node) is not { } argumentList)
        {
            return;
        }

        var arguments = argumentList.Arguments;
        if (arguments.Count < MinimumArguments || !AllNamed(arguments))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsOutOfDeclarationOrder(method, arguments))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(OrderingRules.NamedArgumentOrder, argumentList.GetLocation()));
    }

    /// <summary>Returns whether every argument in the list carries a name.</summary>
    /// <param name="arguments">The argument list.</param>
    /// <returns><see langword="true"/> when all arguments are named.</returns>
    private static bool AllNamed(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether the named arguments are supplied in a different order from the parameters.</summary>
    /// <param name="method">The bound method.</param>
    /// <param name="arguments">The all-named argument list.</param>
    /// <returns><see langword="true"/> only when every name binds and their positions are not ascending.</returns>
    private static bool IsOutOfDeclarationOrder(IMethodSymbol method, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        var lastPosition = -1;
        var outOfOrder = false;
        for (var i = 0; i < arguments.Count; i++)
        {
            var position = ParameterPosition(method, arguments[i].NameColon!.Name.Identifier.ValueText);
            if (position < 0)
            {
                return false;
            }

            if (position < lastPosition)
            {
                outOfOrder = true;
            }

            lastPosition = position;
        }

        return outOfOrder;
    }
}
