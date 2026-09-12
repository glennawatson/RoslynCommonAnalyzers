// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.Text;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a union case declared as a value type (PSH1025). A union is itself a value type, but it holds
/// whichever case it carries in a single object-typed payload, so a value-typed case boxes on every
/// construction.
/// </summary>
/// <remarks>
/// The union declaration syntax only exists in the compiler API from the roslyn5.9 slot, so the rule is
/// compiled in from there. That loses no coverage: a host whose compiler cannot load the 5.9 slot cannot
/// compile a union either, so there is nothing for the rule to find on the lower slots.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1025BoxingUnionCaseAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(AllocationRules.BoxingUnionCase);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

#if ROSLYN_5_9_OR_GREATER
        context.RegisterCompilationStartAction(static start =>
        {
            var reported = new ConcurrentDictionary<(SyntaxTree Tree, TextSpan Span), byte>();
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeUnionDeclaration(nodeContext, reported),
                SyntaxKind.UnionDeclaration);
        });
#endif
    }

#if ROSLYN_5_9_OR_GREATER

    /// <summary>Reports every case of a union declaration whose type is a value type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="reported">The declarations already reported in this compilation.</param>
    /// <remarks>
    /// The experimental union syntax model hands the same declaration to a syntax-node action twice, which
    /// would report every case twice over. Each declaration is claimed once so the count follows the source
    /// rather than the driver.
    /// </remarks>
    private static void AnalyzeUnionDeclaration(
        in SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<(SyntaxTree Tree, TextSpan Span), byte> reported)
    {
        if (((UnionDeclarationSyntax)context.Node).ParameterList is not { } cases
            || !reported.TryAdd((context.Node.SyntaxTree, context.Node.Span), 0))
        {
            return;
        }

        var parameters = cases.Parameters;
        for (var i = 0; i < parameters.Count; i++)
        {
            var caseType = parameters[i].Type;
            if (caseType is null || !CanBeValueType(caseType))
            {
                continue;
            }

            if (context.SemanticModel.GetTypeInfo(caseType, context.CancellationToken).Type is not { IsValueType: true } bound)
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                AllocationRules.BoxingUnionCase,
                caseType.SyntaxTree,
                caseType.Span,
                bound.ToDisplayString()));
        }
    }

    /// <summary>Returns whether a case's spelling leaves it able to be a value type.</summary>
    /// <param name="caseType">The case's type syntax.</param>
    /// <returns><see langword="false"/> when the spelling alone proves the case is a reference type.</returns>
    /// <remarks>
    /// Binding a case type is the whole cost of this rule, and it pulls the compiler's nullable analysis in
    /// behind it. Two spellings settle the question without asking: an array is always a reference type, and
    /// a keyword type cannot be aliased or shadowed, so <c>string</c> and <c>object</c> are references and
    /// every other keyword is a value type. Only a named type still has to be bound.
    /// </remarks>
    private static bool CanBeValueType(TypeSyntax caseType) => caseType switch
    {
        ArrayTypeSyntax => false,
        PredefinedTypeSyntax predefined => predefined.Keyword.RawKind is not (int)SyntaxKind.StringKeyword
            and not (int)SyntaxKind.ObjectKeyword,
        _ => true,
    };

#endif
}
