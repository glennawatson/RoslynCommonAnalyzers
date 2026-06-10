// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a documentation element whose text is copied verbatim from another element in the
/// same documentation comment (SST1625) — for example a parameter whose description repeats
/// the summary. The comparison key includes inline reference targets (a <c>cref</c>, a
/// <c>paramref</c> name, …) so two elements that read the same but point at different references
/// — e.g. parameter descriptions differing only in their <c>&lt;see cref="…"/&gt;</c> — are not
/// mistaken for copies.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1625DuplicateDocumentationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The documentation-comment node kinds the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.SingleLineDocumentationCommentTrivia,
        SyntaxKind.MultiLineDocumentationCommentTrivia);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DocumentationRules.NoDuplicateDocumentation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Reports any documentation element whose text repeats an earlier element's text.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var documentation = (DocumentationCommentTriviaSyntax)context.Node;

        // The buffer is built once and cleared per element; the dedup state stays unallocated until a
        // second non-empty element appears, so the common single-element comment costs no HashSet.
        StringBuilder? builder = null;
        string? firstKey = null;
        HashSet<string>? seen = null;

        foreach (var node in documentation.Content)
        {
            if (node is not XmlElementSyntax element)
            {
                continue;
            }

            builder ??= new StringBuilder();
            builder.Clear();
            XmlDocumentationHelper.AppendDuplicateComparisonKey(element, builder);
            if (builder.Length == 0)
            {
                continue;
            }

            var text = builder.ToString();
            if (firstKey is null)
            {
                firstKey = text;
                continue;
            }

            seen ??= new HashSet<string>(StringComparer.Ordinal) { firstKey };
            if (seen.Add(text))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.NoDuplicateDocumentation, element.GetLocation()));
        }
    }
}
