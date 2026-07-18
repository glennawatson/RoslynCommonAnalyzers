// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>-=</c> whose right operand is a lambda or anonymous method when the left side is an event
/// or a delegate-typed member, local, or parameter (SST2449). A delegate created at the removal site never
/// equals the one added earlier, so the subtraction removes nothing and the original handler stays attached.
/// </summary>
/// <remarks>
/// <para>Three shapes are deliberately not reported:</para>
/// <list type="bullet">
/// <item><description>A right operand that is <b>not a freshly created delegate</b> — a method group, a
/// stored delegate in a field or local, or any other expression. Those can genuinely match the earlier
/// subscription, and whether they do is not decidable here.</description></item>
/// <item><description>A left side that is <b>not an event or delegate</b> — a custom subtraction operator
/// that happens to accept a delegate argument is combining values, not managing a handler
/// list.</description></item>
/// <item><description>A left side with <b>no simple name</b>, such as a delegate array element. The
/// described shape is an event or a delegate-typed field, local, parameter, or property.</description></item>
/// </list>
/// <para>
/// The clean path binds nothing: the right operand's syntax kind is checked first, and almost every
/// <c>-=</c> in real code subtracts a number or a stored delegate, not a lambda. The left side is bound
/// only after a lambda or anonymous method is already on the right.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2449LambdaUnsubscriptionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.LambdaUnsubscription);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SubtractAssignmentExpression);
    }

    /// <summary>Analyzes one <c>-=</c> for a freshly created delegate on its right side.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (Unwrap(assignment.Right) is not AnonymousFunctionExpressionSyntax handler
            || GetTargetName(assignment.Left) is not { } name
            || !RemovesFromHandlerList(context, assignment.Left))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.LambdaUnsubscription,
            handler.GetLocation(),
            name));
    }

    /// <summary>Returns whether the removal target is an event or a delegate-typed value.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="target">The <c>-=</c>'s left-hand side.</param>
    /// <returns><see langword="true"/> when the subtraction removes from a handler list.</returns>
    /// <remarks>
    /// A custom subtraction operator can accept a delegate argument; binding the left side tells a handler
    /// list apart from that. An unresolved target reports nothing — broken code is the compiler's to explain.
    /// </remarks>
    private static bool RemovesFromHandlerList(SyntaxNodeAnalysisContext context, ExpressionSyntax target)
        => context.SemanticModel.GetSymbolInfo(target, context.CancellationToken).Symbol switch
        {
            IEventSymbol => true,
            IFieldSymbol field => IsDelegateType(field.Type),
            ILocalSymbol local => IsDelegateType(local.Type),
            IParameterSymbol parameter => IsDelegateType(parameter.Type),
            IPropertySymbol property => IsDelegateType(property.Type),
            _ => false,
        };

    /// <summary>Returns whether a type is a delegate type.</summary>
    /// <param name="type">The type of the removal target.</param>
    /// <returns><see langword="true"/> when the type is a delegate.</returns>
    private static bool IsDelegateType(ITypeSymbol type) => type.TypeKind == TypeKind.Delegate;

    /// <summary>Gets the name of the event or delegate being removed from.</summary>
    /// <param name="target">The <c>-=</c>'s left-hand side.</param>
    /// <returns>The target's name, or <see langword="null"/> when the target has no simple name.</returns>
    private static string? GetTargetName(ExpressionSyntax target) => target switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax { Name: { } name } => name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Removes any grouping parentheses around an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}
