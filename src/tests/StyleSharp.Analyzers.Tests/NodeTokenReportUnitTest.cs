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

/// <summary>Tests the shared report-a-token-when-the-node-matches helper through a minimal analyzer.</summary>
public sealed class NodeTokenReportUnitTest
{
    /// <summary>The zero-based line of the condition-only loop in <see cref="Source"/>.</summary>
    private const int ConditionOnlyLoopLine = 4;

    /// <summary>Source with one condition-only loop and one loop with every clause.</summary>
    private const string Source =
        """
        class C
        {
            void M(int x)
            {
                for (; x < 10; ) { }
                for (var i = 0; i < 10; i++) { }
            }
        }
        """;

    /// <summary>Verifies a matching node is reported at the selected token and a non-matching node is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReportsTheSelectedTokenOnlyForMatchingNodesAsync()
    {
        var tree = CSharpSyntaxTree.ParseText(Source);
        var compilation = CSharpCompilation.Create(nameof(NodeTokenReportUnitTest), [tree], [RuntimeMetadataReferences.CoreLibrary]);
        var diagnostics = await compilation
            .WithAnalyzers([new ConditionOnlyLoopAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();

        await Assert.That(diagnostics.Length).IsEqualTo(1);
        var reported = (await tree.GetRootAsync()).FindToken(diagnostics[0].Location.SourceSpan.Start);
        await Assert.That(reported.IsKind(SyntaxKind.ForKeyword)).IsTrue();
        await Assert.That(tree.GetLineSpan(reported.Span).StartLinePosition.Line).IsEqualTo(ConditionOnlyLoopLine);
    }

    /// <summary>A minimal analyzer that reports condition-only loops through the helper.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class ConditionOnlyLoopAnalyzer : DiagnosticAnalyzer
    {
        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [ModernSyntaxRules.UseWhileOverFor];

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSyntaxNodeAction(
                static nodeContext => NodeTokenReport.WhenMatches<ForStatementSyntax>(
                    nodeContext,
                    Sst2245UseWhileOverForAnalyzer.IsConditionOnlyLoop,
                    static statement => statement.ForKeyword,
                    ModernSyntaxRules.UseWhileOverFor),
                SyntaxKind.ForStatement);
        }
    }
}
