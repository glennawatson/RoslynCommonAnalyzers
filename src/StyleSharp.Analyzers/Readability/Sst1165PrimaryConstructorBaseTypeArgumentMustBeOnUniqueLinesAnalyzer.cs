// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Analyzer that makes sure that Arguments are on unique lines.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1165PrimaryConstructorBaseTypeArgumentMustBeOnUniqueLinesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The unique diagnostic identifier for this analyzer.</summary>
    internal const string DiagnosticId = "SST1165";

    /// <summary>The diagnostic descriptor for this analyzer.</summary>
    private static readonly DiagnosticDescriptor Rule = UniqueLineRule.ForArguments(DiagnosticId);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        UniqueLineRule.Register<PrimaryConstructorBaseTypeSyntax>(
            context,
            Rule,
            static (nodeContext, node, rule) => nodeContext.HandleArgumentListSyntax(node.ArgumentList, rule),
            SyntaxKind.PrimaryConstructorBaseType);
    }
}
