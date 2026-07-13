// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>goto</c> that jumps to a label (SST2014). A jump between switch sections —
/// <c>goto case</c> and <c>goto default</c> — is not reported: the language offers no other way to say it,
/// and saying it that way is idiomatic.
/// </summary>
/// <remarks>
/// The exclusion is free rather than checked. C# gives the three forms three different syntax kinds, so
/// registering <see cref="SyntaxKind.GotoStatement"/> alone never sees a <c>goto case</c> or a
/// <c>goto default</c> in the first place. There is no code fix: replacing a jump means restructuring the
/// control flow around it, and which structure was meant — a loop, an extracted method, an early return — is
/// not something the jump records.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2014AvoidGotoAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.AvoidGoto);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.GotoStatement);
    }

    /// <summary>Reports one jump to a label.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
        => context.ReportDiagnostic(DiagnosticHelper.Create(ModernizationRules.AvoidGoto, context.Node.GetLocation()));
}
