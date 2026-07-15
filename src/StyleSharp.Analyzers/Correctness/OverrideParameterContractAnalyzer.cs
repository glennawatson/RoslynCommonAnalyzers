// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports where an override's parameter list disagrees with the method it overrides: a parameter default
/// that differs from the base (SST2424), and a <c>params</c> modifier that differs from the base (SST2426).
/// Both are decorations resolved against the static type at the call site, not part of the override itself,
/// so a disagreement either changes a call's meaning silently or states something a reader will act on that
/// is not true.
/// </summary>
/// <remarks>
/// Diagnostics: SST2424, SST2426. The clean path is a single modifier check — a method with no <c>override</c>
/// keyword is rejected before the semantic model is touched. Only an override then fetches its
/// <c>OverriddenMethod</c> (already cached by Roslyn) and walks the two parameter lists once. Parameter
/// defaults are read from the symbol, which reports the override's own value; the <c>params</c> modifier is
/// read from the override's syntax, because the symbol inherits the base's value and would hide the mismatch.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OverrideParameterContractAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        CorrectnessRules.OverrideChangesDefault,
        CorrectnessRules.OverrideChangesParams);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Reports parameter-contract disagreements between an override and its base.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (!ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.OverrideKeyword))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { OverriddenMethod: { } baseMethod } method)
        {
            return;
        }

        var parameters = declaration.ParameterList.Parameters;
        var count = parameters.Count;
        if (count > method.Parameters.Length || count > baseMethod.Parameters.Length)
        {
            return;
        }

        for (var i = 0; i < count; i++)
        {
            var overrideParameter = method.Parameters[i];
            var baseParameter = baseMethod.Parameters[i];

            if (DefaultsDiffer(overrideParameter, baseParameter))
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    CorrectnessRules.OverrideChangesDefault,
                    parameters[i].Identifier.GetLocation(),
                    overrideParameter.Name));
            }

            if (ModifierListHelper.Contains(parameters[i].Modifiers, SyntaxKind.ParamsKeyword) != baseParameter.IsParams)
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    CorrectnessRules.OverrideChangesParams,
                    parameters[i].Identifier.GetLocation(),
                    overrideParameter.Name));
            }
        }
    }

    /// <summary>Returns whether two parameters declare different explicit defaults.</summary>
    /// <param name="overrideParameter">The override's parameter.</param>
    /// <param name="baseParameter">The base method's parameter.</param>
    /// <returns><see langword="true"/> when one has a default the other does not, or the values differ.</returns>
    private static bool DefaultsDiffer(IParameterSymbol overrideParameter, IParameterSymbol baseParameter)
    {
        if (overrideParameter.HasExplicitDefaultValue != baseParameter.HasExplicitDefaultValue)
        {
            return true;
        }

        return overrideParameter.HasExplicitDefaultValue
            && !Equals(overrideParameter.ExplicitDefaultValue, baseParameter.ExplicitDefaultValue);
    }
}
