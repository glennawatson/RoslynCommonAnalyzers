// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Analyzer that requires interface names to begin with the capital letter <c>I</c>
/// (the .NET framework design convention), e.g. <c>ICustomer</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1302InterfaceNamesMustBeginWithIAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The unique diagnostic identifier for this analyzer.</summary>
    internal const string DiagnosticId = "SST1302";

    /// <summary>The category of the diagnostic.</summary>
    private const string Category = "Naming";

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Interface names should begin with I",
        "Interface name '{0}' should begin with 'I'",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Interface names should begin with the capital letter 'I' (for example, ICustomer).",
        helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{DiagnosticId}.md");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(Rule);

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
        if (NamingHelper.BeginsWithCapitalI(identifier.ValueText))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), identifier.ValueText));
    }
}
