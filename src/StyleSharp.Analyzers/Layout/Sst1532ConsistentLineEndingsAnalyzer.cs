// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a file whose line endings do not all match the configured style (SST1532), configured with
/// <c>stylesharp.line_ending</c> (<c>lf</c> | <c>crlf</c>; default <c>lf</c>). One diagnostic is raised
/// per file, anchored on the first line whose ending is wrong; the code fix rewrites every ending in the
/// file. The configured newline rides on the diagnostic so the fix never re-reads configuration.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1532ConsistentLineEndingsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Rule-specific editorconfig key for the file line ending (SST1532).</summary>
    internal const string SpecificKey = "stylesharp.SST1532.line_ending";

    /// <summary>General editorconfig key for the file line ending.</summary>
    internal const string GeneralKey = "stylesharp.line_ending";

    /// <summary>Diagnostic-property key carrying the required newline sequence.</summary>
    internal const string LineEndingProperty = "line_ending";

    /// <summary>The length of a carriage-return/line-feed sequence.</summary>
    private const int CarriageReturnLineFeedLength = 2;

    /// <summary>Diagnostic properties carrying a line-feed requirement.</summary>
    private static readonly ImmutableDictionary<string, string?> LineFeedProperties =
        ImmutableDictionary<string, string?>.Empty.Add(LineEndingProperty, LayoutStyleOptions.LineFeed);

    /// <summary>Diagnostic properties carrying a carriage-return/line-feed requirement.</summary>
    private static readonly ImmutableDictionary<string, string?> CarriageReturnLineFeedProperties =
        ImmutableDictionary<string, string?>.Empty.Add(LineEndingProperty, LayoutStyleOptions.CarriageReturnLineFeed);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.ConsistentLineEndings);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Reports the first line whose ending does not match the configured style.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree);
        var target = LayoutStyleOptions.ReadLineEnding(options, SpecificKey, GeneralKey);
        var text = context.Tree.GetText(context.CancellationToken);
        var lines = text.Lines;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var breakStart = line.End;
            var breakEnd = line.EndIncludingLineBreak;
            if (breakEnd == breakStart || BreakMatches(text, breakStart, breakEnd, target))
            {
                continue;
            }

            var properties = target == LayoutStyleOptions.LineFeed ? LineFeedProperties : CarriageReturnLineFeedProperties;
            var display = target == LayoutStyleOptions.LineFeed ? "LF" : "CRLF";
            var location = Location.Create(context.Tree, TextSpan.FromBounds(line.Start, line.End));
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.ConsistentLineEndings, location, properties, display));
            return;
        }
    }

    /// <summary>Returns whether the line break between two positions equals the target newline sequence.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="breakStart">The start of the line break.</param>
    /// <param name="breakEnd">The end of the line break.</param>
    /// <param name="target">The required newline sequence.</param>
    /// <returns><see langword="true"/> when the break matches the target exactly.</returns>
    private static bool BreakMatches(SourceText text, int breakStart, int breakEnd, string target)
        => target == LayoutStyleOptions.LineFeed
            ? breakEnd == breakStart + 1 && text[breakStart] == '\n'
            : breakEnd - breakStart == CarriageReturnLineFeedLength && text[breakStart] == '\r' && text[breakStart + 1] == '\n';
}
