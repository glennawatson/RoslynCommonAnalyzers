// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports access to a named tuple element through its positional <c>ItemN</c> field instead of
/// its name (SST1142, mirrors SA1142). The no-diagnostic path is a name-shape check, so the
/// semantic model is consulted only for a member access actually named <c>ItemN</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TupleElementNameAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.ReferToTupleElementByName);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleMemberAccessExpression);
    }

    /// <summary>Returns the named tuple element that should replace an <c>ItemN</c> access.</summary>
    /// <param name="access">The candidate member access.</param>
    /// <param name="semanticModel">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="name">The tuple element's preferred name when matched.</param>
    /// <returns><see langword="true"/> when the access should be rewritten.</returns>
    internal static bool TryGetReplacementName(
        MemberAccessExpressionSyntax access,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string? name)
    {
        name = null;
        if (access.Name is not IdentifierNameSyntax identifier
            || !TupleHelpers.TryGetItemPosition(identifier.Identifier.ValueText, out var position)
            || semanticModel.GetTypeInfo(access.Expression, cancellationToken).Type is not INamedTypeSymbol { IsTupleType: true } tuple)
        {
            return false;
        }

        var elements = tuple.TupleElements;
        if (position > elements.Length)
        {
            return false;
        }

        name = elements[position - 1].Name;
        return !string.IsNullOrEmpty(name) && name != identifier.Identifier.ValueText;
    }

    /// <summary>Reports SST1142 when a tuple's named element is accessed through its ItemN field.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (access.Name is not IdentifierNameSyntax identifier
            || !TryGetReplacementName(access, context.SemanticModel, context.CancellationToken, out var name))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ReadabilityRules.ReferToTupleElementByName, identifier.GetLocation(), name!));
    }
}
