// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires parameters to use the .NET camelCase convention (SST1313). Record
/// positional parameters are skipped because they surface as PascalCase
/// properties and are governed by SST1300.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1313ParameterNamingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(NamingRules.ParameterCamelCase);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Parameter);
    }

    /// <summary>Analyzes a parameter for camelCase.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ParameterSyntax parameter)
        {
            return;
        }

        // Record positional parameters become PascalCase properties (SST1300).
        if (parameter.Parent is ParameterListSyntax { Parent: RecordDeclarationSyntax })
        {
            return;
        }

        var identifier = parameter.Identifier;
        var name = identifier.ValueText;
        if (name.Length == 0 || NamingHelper.IsAllUnderscores(name) || NamingHelper.BeginsWithLowerCase(name))
        {
            return;
        }

        NamingDiagnostic.Report(context, NamingRules.ParameterCamelCase, identifier, NamingHelper.SuggestCamelCase(name));
    }
}
