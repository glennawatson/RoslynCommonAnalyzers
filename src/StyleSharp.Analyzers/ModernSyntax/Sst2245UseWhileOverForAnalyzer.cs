// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>for</c> statement that carries a condition but neither an initializer nor an
/// incrementor (SST2245): <c>for (; x &lt; 10; )</c> is a <c>while</c> loop with two empty clauses
/// the reader still has to check.
/// </summary>
/// <remarks>
/// <c>for (;;)</c> keeps its shape: with no condition either, it is the idiomatic infinite loop and
/// has nothing to trade away. A loop with any initializer, any incrementor, or a declaration in the
/// initializer clause is left alone, because those clauses are what a <c>for</c> is for. The check
/// is pure syntax and never binds a symbol.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2245UseWhileOverForAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseWhileOverFor);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeForStatement, SyntaxKind.ForStatement);
    }

    /// <summary>Returns whether a <c>for</c> statement is a <c>while</c> loop wearing empty clauses.</summary>
    /// <param name="statement">The loop to inspect.</param>
    /// <returns><see langword="true"/> when the loop has a condition and nothing else.</returns>
    internal static bool IsConditionOnlyLoop(ForStatementSyntax statement)
        => statement.Condition is not null
            && statement.Declaration is null
            && statement.Initializers.Count == 0
            && statement.Incrementors.Count == 0;

    /// <summary>Reports a condition-only <c>for</c> loop.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeForStatement(SyntaxNodeAnalysisContext context)
    {
        var statement = (ForStatementSyntax)context.Node;
        if (!IsConditionOnlyLoop(statement))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseWhileOverFor, statement.ForKeyword.GetLocation()));
    }
}
