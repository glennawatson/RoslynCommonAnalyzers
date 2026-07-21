// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a documentation code tag whose shape disagrees with its content (SST1661): a single-line snippet
/// wrapped in the block <c>&lt;code&gt;</c> tag, or a multi-line snippet wrapped in the inline <c>&lt;c&gt;</c>
/// tag. The suggested tag is stashed in <see cref="TargetTagKey"/> so the code fix can swap the tag names.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1661CodeTagContentAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic property key holding the tag name the content should use.</summary>
    internal const string TargetTagKey = "targetTag";

    /// <summary>The inline code tag, for a single-line snippet.</summary>
    private const string InlineTag = "c";

    /// <summary>The block code tag, for a multi-line snippet.</summary>
    private const string BlockTag = "code";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DocumentationRules.CodeTagContent);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.XmlElement);
    }

    /// <summary>Reports a <c>&lt;c&gt;</c>/<c>&lt;code&gt;</c> element whose tag does not match its single/multi-line content.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var element = (XmlElementSyntax)context.Node;
        var name = element.StartTag.Name.LocalName.ValueText;
        if (name is not (InlineTag or BlockTag))
        {
            return;
        }

        if (!XmlDocumentationHelper.TryGetFirstTextCharacter(element, out _, out var firstPosition)
            || !XmlDocumentationHelper.TryGetLastTextCharacter(element, out _, out var lastPosition))
        {
            // No text content — an empty tag is not a mismatched snippet.
            return;
        }

        var text = element.SyntaxTree.GetText(context.CancellationToken);
        var multiLine = text.Lines.GetLineFromPosition(firstPosition).LineNumber
            != text.Lines.GetLineFromPosition(lastPosition).LineNumber;

        var (description, target) = (name, multiLine) switch
        {
            (BlockTag, false) => ("single-line", InlineTag),
            (InlineTag, true) => ("multi-line", BlockTag),
            _ => (null, null),
        };

        if (description is null || target is null)
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(TargetTagKey, target);
        context.ReportDiagnostic(DiagnosticHelper.Create(
            DocumentationRules.CodeTagContent,
            element.SyntaxTree,
            element.Span,
            properties,
            description,
            target));
    }
}
