// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a file that does not end with exactly one newline (SST1518): a missing final
/// newline, or trailing blank lines / whitespace after the last line of content.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileEndingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.LineEndingsAtEndOfFile);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Reports the trailing whitespace span when the file does not end with a single newline.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var text = context.Tree.GetText(context.CancellationToken);
        var length = text.Length;
        if (length == 0)
        {
            return;
        }

        var lastContent = length - 1;
        while (lastContent >= 0 && char.IsWhiteSpace(text[lastContent]))
        {
            lastContent--;
        }

        if (lastContent < 0)
        {
            return;
        }

        var tail = text.ToString(TextSpan.FromBounds(lastContent + 1, length));
        if (tail is "\n" or "\r\n")
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.LineEndingsAtEndOfFile, Location.Create(context.Tree, TextSpan.FromBounds(lastContent + 1, length))));
    }
}
