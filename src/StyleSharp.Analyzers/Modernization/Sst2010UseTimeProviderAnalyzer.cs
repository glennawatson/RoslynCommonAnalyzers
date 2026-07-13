// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a type that reads the machine clock directly — <c>DateTime.Now</c>, <c>DateTime.UtcNow</c>,
/// <c>DateTimeOffset.Now</c> or <c>DateTimeOffset.UtcNow</c> — where a <c>TimeProvider</c> could supply the
/// time instead (SST2010). Disabled by default: introducing the seam is a design change, and not every type
/// wants one.
/// </summary>
/// <remarks>
/// <para>
/// <c>TimeProvider</c> arrived in .NET 8. The rule resolves it once per compilation and registers nothing at
/// all when it is absent, so a project that cannot take the advice never sees the diagnostic and never pays
/// for the analysis. There is no code fix: the fix is to accept a <c>TimeProvider</c> and thread it through
/// the callers, which changes the type's construction contract.
/// </para>
/// <para>
/// Only a read inside a type declaration is reported, because a type is the thing that can hold the seam.
/// A top-level statement has no constructor to inject into and nothing to test in isolation, so it is left
/// alone.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2010UseTimeProviderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the seam this rule asks for.</summary>
    private const string TimeProviderMetadataName = "System.TimeProvider";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.UseTimeProvider);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            // The suggestion is only honest where the API exists: no TimeProvider, no rule.
            if (start.Compilation.GetTypeByMetadataName(TimeProviderMetadataName) is null)
            {
                return;
            }

            var clockTypes = ClockPropertyAccess.ClockTypes.Resolve(start.Compilation);
            if (!clockTypes.Any)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, clockTypes),
                SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    /// <summary>Reports one direct clock read inside a type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="clockTypes">The clock types resolved for this compilation.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, in ClockPropertyAccess.ClockTypes clockTypes)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (!ClockPropertyAccess.MatchesSpelling(access, localOnly: false) || !IsInsideTypeDeclaration(access))
        {
            return;
        }

        if (!ClockPropertyAccess.BindsToClock(context.SemanticModel, access, clockTypes, context.CancellationToken))
        {
            return;
        }

        var diagnostic = DiagnosticHelper.Create(
            ModernizationRules.UseTimeProvider,
            access.GetLocation(),
            ClockPropertyAccess.Describe(access));
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>Returns whether an expression sits inside a type that could hold a <c>TimeProvider</c>.</summary>
    /// <param name="node">The clock read.</param>
    /// <returns><see langword="true"/> when a type declaration encloses the read.</returns>
    private static bool IsInsideTypeDeclaration(SyntaxNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current is TypeDeclarationSyntax)
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }
}
