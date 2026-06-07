// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a property, indexer, or event whose block-bodied accessors mix single-line and
/// multi-line forms (SST1504). Either every block-bodied accessor is written on a single
/// line, or every one spans multiple lines. Auto and expression-bodied accessors are ignored.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AccessorConsistencyAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.AccessorLineConsistency);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AccessorList);
    }

    /// <summary>Reports an accessor list that contains both a single-line and a multi-line block accessor.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var list = (AccessorListSyntax)context.Node;
        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);

        var sawSingleLine = false;
        var sawMultiLine = false;
        foreach (var accessor in list.Accessors)
        {
            if (accessor.Body is not { } body)
            {
                continue;
            }

            if (LayoutHelpers.StartLine(text, body.OpenBraceToken) == LayoutHelpers.StartLine(text, body.CloseBraceToken))
            {
                sawSingleLine = true;
            }
            else
            {
                sawMultiLine = true;
            }
        }

        if (!sawSingleLine || !sawMultiLine)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.AccessorLineConsistency, list.OpenBraceToken.GetLocation()));
    }
}
