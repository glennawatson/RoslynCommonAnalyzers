// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the parts of a partial class that omit <c>static</c> while another part declares it (SST2335). The
/// type still compiles as static — the part that names <c>static</c> decides it — but a reader looking at a
/// part without the keyword sees an ordinary class and can waste time trying to give it instance state.
/// </summary>
/// <remarks>
/// This is a house-style consistency nudge rather than a defect, so it is disabled by default and opt-in
/// through <c>.editorconfig</c>. It fires only when at least one part carries <c>static</c> and at least one
/// does not, and reports each part that omits it. The clean path is a type-kind check followed by a
/// declaration count, so a non-partial class costs almost nothing.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2335PartialStaticMismatchAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DesignRules.PartialTypeStaticModifierMismatch);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    /// <summary>Reports each part of a partial class that disagrees with another on <c>static</c>.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var references = type.DeclaringSyntaxReferences;
        if (type.TypeKind != TypeKind.Class || references.Length < 2)
        {
            return;
        }

        var anyStatic = false;
        var anyMissing = false;
        for (var i = 0; i < references.Length; i++)
        {
            if (references[i].GetSyntax(context.CancellationToken) is not ClassDeclarationSyntax declaration)
            {
                continue;
            }

            if (ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.StaticKeyword))
            {
                anyStatic = true;
            }
            else
            {
                anyMissing = true;
            }
        }

        if (!anyStatic || !anyMissing)
        {
            return;
        }

        ReportPartsMissingStatic(context, type.Name, references);
    }

    /// <summary>Reports the identifier of each part that omits <c>static</c>.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="typeName">The type's name.</param>
    /// <param name="references">The type's declaring syntax references.</param>
    private static void ReportPartsMissingStatic(SymbolAnalysisContext context, string typeName, ImmutableArray<SyntaxReference> references)
    {
        for (var i = 0; i < references.Length; i++)
        {
            if (references[i].GetSyntax(context.CancellationToken) is ClassDeclarationSyntax declaration
                && !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.StaticKeyword))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DesignRules.PartialTypeStaticModifierMismatch,
                    declaration.Identifier.GetLocation(),
                    typeName));
            }
        }
    }
}
