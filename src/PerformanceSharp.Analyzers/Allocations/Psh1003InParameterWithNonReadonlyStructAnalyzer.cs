// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports <c>in</c> (and C# 12 <c>ref readonly</c>) parameters whose type is a struct
/// that is not declared <c>readonly</c> (PSH1003). The compiler cannot prove that member
/// accesses on such a parameter leave it unmodified, so it emits a hidden defensive copy
/// of the whole struct before each one — quietly costing more than passing by value.
/// The check is syntax-first: a parameter without an <c>in</c> modifier or the
/// <c>ref readonly</c> pair is rejected before any semantic work.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1003InParameterWithNonReadonlyStructAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.InParameterWithNonReadonlyStruct);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
    }

    /// <summary>Reports PSH1003 for a readonly-reference parameter whose struct type is not readonly.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
    {
        var parameter = (ParameterSyntax)context.Node;
        if (!IsReadonlyReferenceParameter(parameter.Modifiers))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) is not
            { Type: INamedTypeSymbol { TypeKind: TypeKind.Struct, SpecialType: SpecialType.None, IsReadOnly: false } type })
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.InParameterWithNonReadonlyStruct,
            parameter.Identifier.GetLocation(),
            parameter.Identifier.ValueText,
            type.ToMinimalDisplayString(context.SemanticModel, parameter.SpanStart)));
    }

    /// <summary>Returns whether a parameter modifier list contains <c>in</c> or the <c>ref readonly</c> pair.</summary>
    /// <param name="modifiers">The parameter modifier list.</param>
    /// <returns><see langword="true"/> when the parameter is passed by readonly reference.</returns>
    private static bool IsReadonlyReferenceParameter(SyntaxTokenList modifiers)
    {
        var hasRef = false;
        var hasReadonly = false;
        for (var i = 0; i < modifiers.Count; i++)
        {
            switch (modifiers[i].Kind())
            {
                case SyntaxKind.InKeyword:
                    return true;

                case SyntaxKind.RefKeyword:
                {
                    hasRef = true;
                    break;
                }

                case SyntaxKind.ReadOnlyKeyword:
                {
                    hasReadonly = true;
                    break;
                }
            }
        }

        return hasRef && hasReadonly;
    }
}
