// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared line-placement checks and reports of the layout rules.</summary>
public class LayoutBreakPlacementTests
{
    /// <summary>The rule-specific key the probe reads.</summary>
    private const string RuleKey = "stylesharp.TEST.placement";

    /// <summary>The project-wide key the probe reads.</summary>
    private const string GeneralKey = "stylesharp.placement";

    /// <summary>The number of reports the probe makes for one misplaced token, one per report shape.</summary>
    private const int ReportsPerMisplacedToken = 2;

    /// <summary>Verifies a token is on its predecessor's line only when both sit on the same line.</summary>
    /// <param name="source">The class declaration.</param>
    /// <param name="expected">Whether the base list colon shares the identifier's line.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C : object { }", true)]
    [Arguments("class C\n    : object { }", false)]
    [Arguments("class C /* comment */ : object { }", true)]
    public async Task SharesLineComparesTheTokenWithItsPredecessorAsync(string source, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var type = (ClassDeclarationSyntax)(await tree.GetRootAsync()).ChildNodes().First();

        await Assert.That(LayoutHelpers.SharesLineWithPreviousToken(tree, type.BaseList!.ColonToken, CancellationToken.None)).IsEqualTo(expected);
    }

    /// <summary>Verifies the first token of a file has no predecessor to share a line with.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FirstTokenHasNoPredecessorAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { }");
        var first = (await tree.GetRootAsync()).GetFirstToken();

        await Assert.That(LayoutHelpers.SharesLineWithPreviousToken(tree, first, CancellationToken.None)).IsFalse();
    }

    /// <summary>Verifies the message side names the start or the end of the line.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PlacementSideNamesTheLineEndAsync()
    {
        await Assert.That(LayoutHelpers.PlacementSide(wantBreakBefore: true)).IsEqualTo("start");
        await Assert.That(LayoutHelpers.PlacementSide(wantBreakBefore: false)).IsEqualTo("end");
    }

    /// <summary>Verifies the configured placement, then the default, decides which side of the break is wrong.</summary>
    /// <param name="ruleValue">The rule-specific value, or <see langword="null"/> when unset.</param>
    /// <param name="generalValue">The project-wide value, or <see langword="null"/> when unset.</param>
    /// <param name="source">The wrapped addition.</param>
    /// <param name="expectedSide">The side the report names, or <see langword="null"/> when nothing is reported.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(null, null, "class C { int M(int a, int b) => a\n    + b; }", null)]
    [Arguments(null, null, "class C { int M(int a, int b) => a +\n    b; }", "start")]
    [Arguments("after", null, "class C { int M(int a, int b) => a\n    + b; }", "end")]
    [Arguments(null, "after", "class C { int M(int a, int b) => a +\n    b; }", null)]
    [Arguments("before", "after", "class C { int M(int a, int b) => a +\n    b; }", "start")]
    public async Task ConfiguredPlacementDecidesTheWrongSideAsync(string? ruleValue, string? generalValue, string source, string? expectedSide)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(nameof(LayoutBreakPlacementTests), [tree], [RuntimeMetadataReferences.CoreLibrary]);
        var options = new AnalyzerOptions([], new PlacementOptionsProvider(new PlacementOptions(ruleValue, generalValue)));
        var diagnostics = await compilation.WithAnalyzers([new PlacementProbeAnalyzer()], options).GetAnalyzerDiagnosticsAsync();

        if (expectedSide is null)
        {
            await Assert.That(diagnostics).IsEmpty();
            return;
        }

        await Assert.That(diagnostics.Length).IsEqualTo(ReportsPerMisplacedToken);
        for (var i = 0; i < diagnostics.Length; i++)
        {
            var diagnostic = diagnostics[i];
            var expectedProperty = expectedSide == "start" ? "true" : "false";
            await Assert.That(diagnostic.Properties[LayoutHelpers.BreakBeforeProperty]).IsEqualTo(expectedProperty);
            await Assert.That(diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).EndsWith(expectedSide);
        }
    }

    /// <summary>Runs the placement check in the real analyzer driver and reports through both report shapes.</summary>
    private sealed class PlacementProbeAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>The report that names only the side.</summary>
        private static readonly DiagnosticDescriptor SideOnly = new("TEST0001", "Side", "Break at the {0}", "Tests", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        /// <summary>The report that names the token and the side.</summary>
        private static readonly DiagnosticDescriptor TokenAndSide = new("TEST0002", "Token and side", "Place '{0}' at the {1}", "Tests", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [SideOnly, TokenAndSide];

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AddExpression);
        }

        /// <summary>Reports the addition operator when it breaks on the unwanted side.</summary>
        /// <param name="context">The driver-provided syntax context.</param>
        private static void Analyze(SyntaxNodeAnalysisContext context)
        {
            var op = ((BinaryExpressionSyntax)context.Node).OperatorToken;
            var breakBefore = LayoutHelpers.HasLineBreakBefore(op);
            var breakAfter = LayoutHelpers.HasLineBreakAfter(op);
            if (!LayoutHelpers.IsBreakMisplaced(context, RuleKey, GeneralKey, defaultBreakBefore: true, breakBefore, breakAfter, out var wantBreakBefore))
            {
                return;
            }

            LayoutHelpers.ReportMisplacedBreak(context, SideOnly, op, wantBreakBefore);
            LayoutHelpers.ReportMisplacedBreak(context, TokenAndSide, op, op.Text, wantBreakBefore);
        }
    }

    /// <summary>Hands the same placement options to every tree.</summary>
    /// <param name="options">The placement options.</param>
    private sealed class PlacementOptionsProvider(AnalyzerConfigOptions options) : AnalyzerConfigOptionsProvider
    {
        /// <inheritdoc/>
        public override AnalyzerConfigOptions GlobalOptions => options;

        /// <inheritdoc/>
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => options;

        /// <inheritdoc/>
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => options;
    }

    /// <summary>Supplies the rule-specific and project-wide placement values.</summary>
    /// <param name="ruleValue">The rule-specific value, or <see langword="null"/> when unset.</param>
    /// <param name="generalValue">The project-wide value, or <see langword="null"/> when unset.</param>
    private sealed class PlacementOptions(string? ruleValue, string? generalValue) : AnalyzerConfigOptions
    {
        /// <inheritdoc/>
        public override bool TryGetValue(string key, out string value)
        {
            var configured = key switch
            {
                RuleKey => ruleValue,
                GeneralKey => generalValue,
                _ => null,
            };
            value = configured ?? string.Empty;
            return configured is not null;
        }
    }
}
