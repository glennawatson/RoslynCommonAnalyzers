// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports tail-position using blocks that can use a using declaration without extending lifetime (SST2236).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2236UsingDeclarationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 8 language-version value.</summary>
    private const int CSharp8 = 800;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseUsingDeclaration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeUsingStatement, SyntaxKind.UsingStatement);
    }

    /// <summary>Reports using blocks that are the last statement in their parent block.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeUsingStatement(SyntaxNodeAnalysisContext context)
    {
        var usingStatement = (UsingStatementSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(usingStatement, CSharp8)
            || usingStatement.Declaration is null
            || usingStatement.Statement is not BlockSyntax
            || usingStatement.Parent is not BlockSyntax block
            || block.Statements.Count == 0
            || block.Statements[block.Statements.Count - 1] != usingStatement)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseUsingDeclaration, usingStatement.UsingKeyword.GetLocation()));
    }

    /// <summary>Returns whether the syntax tree uses at least the supplied language version.</summary>
    /// <param name="node">The syntax node.</param>
    /// <param name="version">The numeric language version.</param>
    /// <returns><see langword="true"/> when the feature is available.</returns>
    private static bool IsLanguageVersionAtLeast(SyntaxNode node, int version)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= version;
}
