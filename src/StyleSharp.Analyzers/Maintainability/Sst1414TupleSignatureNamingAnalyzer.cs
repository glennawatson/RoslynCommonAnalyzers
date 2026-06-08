// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a tuple type in a member signature that has an unnamed element (SST1414, mirrors
/// SA1414). A tuple type that appears inside a statement (a local) is a deliberately separate
/// case and is not reported; the check is purely syntactic.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1414TupleSignatureNamingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.TupleSignatureElementNames);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.TupleType);
    }

    /// <summary>Reports SST1414 when a signature tuple type has an unnamed element.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = (TupleTypeSyntax)context.Node;
        if (!HasUnnamedElement(node) || node.FirstAncestorOrSelf<StatementSyntax>() is not null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.TupleSignatureElementNames, node.GetLocation()));
    }

    /// <summary>Returns whether any element of the tuple type lacks a name.</summary>
    /// <param name="node">The tuple type.</param>
    /// <returns><see langword="true"/> when at least one element has no identifier.</returns>
    private static bool HasUnnamedElement(TupleTypeSyntax node)
    {
        var elements = node.Elements;
        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i].Identifier.IsKind(SyntaxKind.None))
            {
                return true;
            }
        }

        return false;
    }
}
