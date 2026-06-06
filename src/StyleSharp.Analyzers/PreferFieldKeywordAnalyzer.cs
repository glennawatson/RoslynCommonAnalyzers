// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Suggests the C# 14 <c>field</c> keyword for a single-use backing field with accessor logic (SST2200).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferFieldKeywordAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 14 language-version value.</summary>
    private const int CSharp14 = 1400;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.PreferFieldKeyword);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Reports a property with a non-trivial single-use backing field.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (property.SyntaxTree.Options is not CSharpParseOptions options
            || (int)options.LanguageVersion < CSharp14
            || property.Modifiers.Any(SyntaxKind.StaticKeyword)
            || property.ExplicitInterfaceSpecifier is not null
            || !FieldReferenceAnalysis.TryFindSingleUseBackingField(
                context.SemanticModel,
                property,
                context.CancellationToken,
                out _,
                out _,
                out var field)
            || TrivialAutoPropertyAnalyzer.HasOnlyTrivialAccessors(
                context.SemanticModel,
                property,
                field!,
                context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.PreferFieldKeyword, property.Identifier.GetLocation()));
    }
}
