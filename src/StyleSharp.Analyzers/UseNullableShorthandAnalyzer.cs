// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a nullable value type written in long form (<c>Nullable&lt;T&gt;</c>) instead of the
/// <c>T?</c> shorthand (SST1125). The semantic model confirms the type is <c>System.Nullable&lt;T&gt;</c>
/// so a user-defined type that happens to be called <c>Nullable</c> is never flagged.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseNullableShorthandAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.UseNullableShorthand);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.GenericName);
    }

    /// <summary>Reports a long-form <c>Nullable&lt;T&gt;</c> type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var generic = (GenericNameSyntax)context.Node;
        if (generic.Identifier.ValueText != "Nullable" || generic.TypeArgumentList.Arguments.Count != 1)
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(generic, context.CancellationToken).Type is not INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseNullableShorthand, OuterTypeNode(generic).GetLocation()));
    }

    /// <summary>Returns the full type node to replace, climbing past a qualifying name such as <c>System.Nullable&lt;T&gt;</c>.</summary>
    /// <param name="generic">The <c>Nullable&lt;T&gt;</c> generic name.</param>
    /// <returns>The outermost type syntax node covering the nullable type.</returns>
    private static SyntaxNode OuterTypeNode(GenericNameSyntax generic)
    {
        SyntaxNode node = generic;
        while (node.Parent is QualifiedNameSyntax qualified && qualified.Right == node)
        {
            node = qualified;
        }

        return node;
    }
}
