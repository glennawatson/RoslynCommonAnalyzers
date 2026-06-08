// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires every file to begin with the header configured by the
/// <c>file_header_template</c> editorconfig option (SST1633). When the option is
/// <c>unset</c> or empty the rule does nothing. Unlike the SDK's the rule, this is
/// a normal analyzer, so a configured header is enforced in ordinary builds
/// without turning on <c>EnforceCodeStyleInBuild</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1633FileHeaderAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DocumentationRules.FileHeader);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Analyzes a file for its configured header.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree);
        if (!FileHeaderHelper.TryGetTemplate(options, out var template))
        {
            return;
        }

        var rendered = FileHeaderHelper.Render(template, context.Tree.FilePath);
        if (HeaderMatches(context.Tree.GetRoot(context.CancellationToken), rendered))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(FileHeaderHelper.HeaderProperty, rendered);
        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.FileHeader, Location.Create(context.Tree, new(0, 0)), properties));
    }

    /// <summary>Returns whether the file's leading comments match the rendered header.</summary>
    /// <param name="root">The compilation unit.</param>
    /// <param name="rendered">The rendered header (lines joined by "\n").</param>
    /// <returns><see langword="true"/> when the header is present.</returns>
    private static bool HeaderMatches(SyntaxNode root, string rendered)
    {
        var expected = rendered.Split('\n');
        var index = 0;

        foreach (var trivia in root.GetLeadingTrivia())
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                if (index >= expected.Length)
                {
                    return true;
                }

                if (!string.Equals(trivia.ToString().TrimEnd(), expected[index], StringComparison.Ordinal))
                {
                    return false;
                }

                index++;
            }
            else if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                break;
            }
        }

        return index >= expected.Length;
    }
}
