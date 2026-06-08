// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a documentation element whose text is copied verbatim from another element in the
/// same documentation comment (SST1625) — for example a parameter whose description repeats
/// the summary.
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
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in documentation.Content)
        {
            if (node is not XmlElementSyntax element)
            {
                continue;
            }

            var text = XmlDocumentationHelper.NormalizedText(element);
            if (text.Length == 0 || seen.Add(text))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.NoDuplicateDocumentation, element.GetLocation()));
        }
    }
}
