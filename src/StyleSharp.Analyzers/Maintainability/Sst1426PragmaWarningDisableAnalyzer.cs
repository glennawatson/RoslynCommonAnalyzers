// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>#pragma warning disable</c> directive that silences an analyzer warning (SST1426),
/// where a scoped <c>[SuppressMessage]</c> attribute would be reviewable and bounded to a single
/// declaration. A directive whose codes are <em>all</em> compiler warnings (<c>CS####</c> or a bare
/// numeric code) is left alone, because the compiler only honours <c>#pragma</c> for those.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1426PragmaWarningDisableAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.PreferSuppressMessageOverPragma);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Reports each <c>#pragma warning disable</c> directive that names a suppressible analyzer code.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        if (!root.ContainsDirectives)
        {
            return;
        }

        for (var directive = root.GetFirstDirective(); directive is not null; directive = directive.GetNextDirective())
        {
            if (directive is not PragmaWarningDirectiveTriviaSyntax pragma
                || !pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            {
                continue;
            }

            if (PragmaWarningHelper.GetSuppressibleCodeList(pragma) is not { } codeList)
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(MaintainabilityRules.PreferSuppressMessageOverPragma, pragma.GetLocation(), codeList));
        }
    }
}
