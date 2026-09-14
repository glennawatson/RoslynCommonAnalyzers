// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a generic type-constraint clause (<c>where T : …</c>) that shares its line with the
/// declaration or a previous constraint (SST1127). One constraint per line keeps long generic
/// signatures readable.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1127ConstraintOnOwnLineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ReadabilityRules.ConstraintOnOwnLine);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            static nodeContext => LayoutHelpers.ReportWhenSharesLineWithPreviousToken(
                nodeContext,
                ((TypeParameterConstraintClauseSyntax)nodeContext.Node).WhereKeyword,
                ReadabilityRules.ConstraintOnOwnLine),
            SyntaxKind.TypeParameterConstraintClause);
    }
}
