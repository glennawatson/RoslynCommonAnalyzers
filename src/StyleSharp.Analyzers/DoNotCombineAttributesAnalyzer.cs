// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an attribute list that combines several attributes in one bracket pair (<c>[A, B]</c>)
/// (SST1133). Writing each attribute in its own <c>[...]</c> keeps diffs small when attributes are
/// added or removed and lets each carry its own documentation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DoNotCombineAttributesAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.DoNotCombineAttributes);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AttributeList);
    }

    /// <summary>Reports each attribute beyond the first in a combined bracket list.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var attributes = ((AttributeListSyntax)context.Node).Attributes;
        if (attributes.Count < 2)
        {
            return;
        }

        for (var index = 1; index < attributes.Count; index++)
        {
            context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.DoNotCombineAttributes, attributes[index].GetLocation()));
        }
    }
}
