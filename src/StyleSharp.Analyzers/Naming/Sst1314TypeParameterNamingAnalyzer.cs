// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Requires generic type parameters to begin with the capital letter T (SST1314), e.g. TKey.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1314TypeParameterNamingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(NamingRules.TypeParameterT);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.TypeParameter);
    }

    /// <summary>Analyzes a type parameter for the leading 'T'.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not TypeParameterSyntax typeParameter)
        {
            return;
        }

        var identifier = typeParameter.Identifier;
        var name = identifier.ValueText;
        if (NamingHelper.IsAllUnderscores(name) || NamingHelper.BeginsWithCapitalT(name))
        {
            return;
        }

        NamingDiagnostic.Report(context, NamingRules.TypeParameterT, identifier, NamingHelper.SuggestPrefixed(name, 'T'));
    }
}
