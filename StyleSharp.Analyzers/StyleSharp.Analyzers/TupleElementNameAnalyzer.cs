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
    /// <summary>The diagnostic property key holding the tuple element's name.</summary>
    internal const string NameKey = "Name";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.ReferToTupleElementByName);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleMemberAccessExpression);
    }

    /// <summary>Reports SST1142 when a tuple's named element is accessed through its ItemN field.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (access.Name is not IdentifierNameSyntax identifier
            || !TupleHelpers.TryGetItemPosition(identifier.Identifier.ValueText, out var position))
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type is not INamedTypeSymbol { IsTupleType: true } tuple)
        {
            return;
        }

        var elements = tuple.TupleElements;
        if (position > elements.Length)
        {
            return;
        }

        var name = elements[position - 1].Name;
        if (string.IsNullOrEmpty(name) || name == identifier.Identifier.ValueText)
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(NameKey, name);
        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.ReferToTupleElementByName, identifier.GetLocation(), properties, name, identifier.Identifier.ValueText));
    }
}
