// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Suggests the C# 14 <c>field</c> keyword for a single-use backing field with accessor logic (SST2200).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2200PreferFieldKeywordAnalyzer : DiagnosticAnalyzer
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
    /// <remarks>
    /// The syntactic prepass rejects the common shape for free. When every accessor is a bare read or write
    /// of one name, the only field the semantic search could find is that name's, and its accessors are by
    /// construction trivial — so the rule would bail after the search anyway. Short-circuiting here keeps a
    /// property that SST1420 already owns from ever touching the semantic model.
    /// </remarks>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        // A property without an accessor list has no accessor logic worth keeping, so SST1420 converts it
        // to an auto-property rather than this rule steering it toward a field-keyword backing store.
        var property = (PropertyDeclarationSyntax)context.Node;
        if (property.SyntaxTree.Options is not CSharpParseOptions options
            || (int)options.LanguageVersion < CSharp14
            || ModifierListHelper.Contains(property.Modifiers, SyntaxKind.StaticKeyword)
            || property.AccessorList is null
            || property.ExplicitInterfaceSpecifier is not null
            || Sst1420TrivialAutoPropertyAnalyzer.TryGetSingleBackingFieldName(property, out _)
            || !FieldReferenceAnalysis.TryFindSingleUseBackingField(
                context.SemanticModel,
                property,
                context.CancellationToken,
                out _,
                out _,
                out var field)
            || Sst1420TrivialAutoPropertyAnalyzer.HasOnlyTrivialAccessors(
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
