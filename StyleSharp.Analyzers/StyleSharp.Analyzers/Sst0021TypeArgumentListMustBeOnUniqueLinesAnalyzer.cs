// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Analyzer that makes sure that Arguments are on unique lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst0021TypeArgumentListMustBeOnUniqueLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The unique diagnostic identifier for this analyzer.</summary>
    internal const string DiagnosticId = "SST0021";

    /// <summary>The category of the diagnostic.</summary>
    private const string Category = "Readability";

    /// <summary>The localized title of the diagnostic.</summary>
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerTitle), Resources.ResourceManager, typeof(Resources));

    /// <summary>The localized message format of the diagnostic.</summary>
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    /// <summary>The localized description of the diagnostic.</summary>
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.ArgumentAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{DiagnosticId}.md");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.TypeArgumentList);
    }

    /// <summary>Analyzes the supplied syntax node and reports the diagnostic when required.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not TypeArgumentListSyntax node)
        {
            return;
        }

        context.HandleTypeArgumentListSyntax(node, Rule);
    }
}
