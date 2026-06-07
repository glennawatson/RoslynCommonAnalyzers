// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Analyzer that requires interface names to begin with the capital letter <c>I</c>
/// (the .NET framework design convention), e.g. <c>ICustomer</c> (SST1302).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1302InterfaceNamesMustBeginWithIAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(NamingRules.InterfaceI);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InterfaceDeclaration);
    }

    /// <summary>Analyzes the supplied interface declaration and reports when its name does not begin with 'I'.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InterfaceDeclarationSyntax declaration)
        {
            return;
        }

        var identifier = declaration.Identifier;
        var name = identifier.ValueText;
        if (NamingHelper.BeginsWithCapitalI(name))
        {
            return;
        }

        NamingDiagnostic.Report(context, NamingRules.InterfaceI, identifier, NamingHelper.SuggestPrefixed(name, 'I'));
    }
}
