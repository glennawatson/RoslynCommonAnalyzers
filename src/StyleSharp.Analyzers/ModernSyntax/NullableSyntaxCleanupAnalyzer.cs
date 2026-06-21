// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports nullable syntax that no longer changes flow or file-local nullable state.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullableSyntaxCleanupAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ModernSyntaxRules.RemoveUnneededNullForgiving,
        ModernSyntaxRules.RemoveRepeatedNullableDirective,
        ModernSyntaxRules.RemoveUnusedNullableRestore);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeNullForgiving, SyntaxKind.SuppressNullableWarningExpression);
        context.RegisterSyntaxTreeAction(AnalyzeNullableDirectives);
    }

    /// <summary>Reports a null-forgiving operator that is applied to an already non-null expression.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeNullForgiving(SyntaxNodeAnalysisContext context)
    {
        var suppression = (PostfixUnaryExpressionSyntax)context.Node;
        var typeInfo = context.SemanticModel.GetTypeInfo(suppression.Operand, context.CancellationToken);
        if (!IsAlreadyNonNull(typeInfo, suppression.Operand, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.RemoveUnneededNullForgiving, suppression.OperatorToken.GetLocation()));
    }

    /// <summary>Reports repeated nullable directives in file order.</summary>
    /// <param name="context">The syntax tree context.</param>
    private static void AnalyzeNullableDirectives(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        string? currentState = null;
        var sawFileStateChange = false;

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            if (trivia.GetStructure() is not NullableDirectiveTriviaSyntax directive)
            {
                continue;
            }

            var setting = directive.SettingToken.ValueText;
            if (setting == "restore")
            {
                if (!sawFileStateChange)
                {
                    context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.RemoveUnusedNullableRestore, directive.GetLocation()));
                }

                currentState = null;
                continue;
            }

            sawFileStateChange = true;
            var state = StateKey(directive);
            if (currentState == state)
            {
                context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.RemoveRepeatedNullableDirective, directive.GetLocation()));
            }

            currentState = state;
        }
    }

    /// <summary>Returns whether nullable flow already treats the operand as non-null.</summary>
    /// <param name="typeInfo">The operand type information.</param>
    /// <param name="operand">The suppressed expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when suppressing nullability has no effect.</returns>
    private static bool IsAlreadyNonNull(
        TypeInfo typeInfo,
        ExpressionSyntax operand,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (typeInfo.Type is { IsValueType: true, OriginalDefinition.SpecialType: not SpecialType.System_Nullable_T })
        {
            return true;
        }

        if (DeclaredNullableAnnotation(operand, model, cancellationToken) == NullableAnnotation.Annotated)
        {
            return false;
        }

        return typeInfo.Nullability.FlowState == NullableFlowState.NotNull
            && typeInfo.Nullability.Annotation == NullableAnnotation.NotAnnotated;
    }

    /// <summary>Gets the declared nullable annotation for a simple symbol operand.</summary>
    /// <param name="operand">The operand expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns>The symbol's declared nullable annotation, or <see langword="null"/>.</returns>
    private static NullableAnnotation? DeclaredNullableAnnotation(
        ExpressionSyntax operand,
        SemanticModel model,
        CancellationToken cancellationToken)
        => model.GetSymbolInfo(operand, cancellationToken).Symbol switch
        {
            ILocalSymbol local => local.NullableAnnotation,
            IParameterSymbol parameter => parameter.NullableAnnotation,
            IFieldSymbol field => field.NullableAnnotation,
            IPropertySymbol property => property.NullableAnnotation,
            _ => null
        };

    /// <summary>Builds a compact comparable key for a nullable directive state.</summary>
    /// <param name="directive">The nullable directive.</param>
    /// <returns>The directive state key.</returns>
    private static string StateKey(NullableDirectiveTriviaSyntax directive)
        => directive.TargetToken.ValueText.Length == 0
            ? directive.SettingToken.ValueText
            : $"{directive.SettingToken.ValueText}:{directive.TargetToken.ValueText}";
}
