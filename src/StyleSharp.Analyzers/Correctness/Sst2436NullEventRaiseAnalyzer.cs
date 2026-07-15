// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an instance event raised with a null sender or null event args (SST2436):
/// <c>Changed?.Invoke(null, e)</c> or <c>Changed?.Invoke(this, null)</c>. Every subscriber that reads the
/// value it was handed throws a <see cref="NullReferenceException"/>, and the stack trace lands in the
/// subscriber rather than the code that raised the event.
/// </summary>
/// <remarks>
/// The prepass ends analysis for almost every call before the semantic model is touched: the invocation must
/// have exactly two arguments, name <c>Invoke</c>, and pass a syntactic null literal (<c>null</c> or
/// <c>null!</c>). Only then does the rule bind, confirm the target is a delegate <c>Invoke</c> of the
/// <c>(object sender, EventArgs args)</c> shape whose receiver is an event, and check the exemptions: a static
/// event may take a null sender, and <c>EventArgs.Empty</c> is the fix rather than the bug. <c>System.EventArgs</c>
/// is resolved once at compilation start.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2436NullEventRaiseAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name a delegate invocation must carry to be an event raise.</summary>
    private const string InvokeName = "Invoke";

    /// <summary>The message clause for the sender role.</summary>
    private const string SenderRole = "sender";

    /// <summary>The message clause for the event-args role.</summary>
    private const string EventArgsRole = "event args";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.NullEventRaise);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var eventArgsType = start.Compilation.GetTypeByMetadataName("System.EventArgs");
            if (eventArgsType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, eventArgsType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Analyzes one invocation for a null-sender or null-args event raise.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="eventArgsType">The resolved <c>System.EventArgs</c> symbol.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol eventArgsType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count != 2 || InvokedName(invocation) != InvokeName)
        {
            return;
        }

        var senderIsNull = IsNullLiteral(arguments[0].Expression);
        var argsIsNull = IsNullLiteral(arguments[1].Expression);
        if (!senderIsNull && !argsIsNull)
        {
            return;
        }

        if (!TryGetRaisedEvent(context, invocation, eventArgsType, out var raisedEvent, out var invoke))
        {
            return;
        }

        if (senderIsNull && !raisedEvent.IsStatic)
        {
            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.NullEventRaise,
                arguments[0].Expression.GetLocation(),
                raisedEvent.Name,
                SenderRole,
                "this"));
        }

        if (!argsIsNull)
        {
            return;
        }

        var suggestion = SymbolEqualityComparer.Default.Equals(invoke.Parameters[1].Type, eventArgsType)
            ? "EventArgs.Empty"
            : "a real event-args instance";
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.NullEventRaise,
            arguments[1].Expression.GetLocation(),
            raisedEvent.Name,
            EventArgsRole,
            suggestion));
    }

    /// <summary>Binds an invocation to the event it raises when it is a well-formed event-handler <c>Invoke</c>.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="invocation">The invocation.</param>
    /// <param name="eventArgsType">The resolved <c>System.EventArgs</c> symbol.</param>
    /// <param name="raisedEvent">The event being raised, when the call is an event raise.</param>
    /// <param name="invoke">The bound delegate <c>Invoke</c>, when the call is an event raise.</param>
    /// <returns><see langword="true"/> when the invocation raises an instance-or-static event.</returns>
    private static bool TryGetRaisedEvent(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol eventArgsType,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IEventSymbol? raisedEvent,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IMethodSymbol? invoke)
    {
        raisedEvent = null;
        invoke = null;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.DelegateInvoke } bound
            || !IsEventArgsShape(bound, eventArgsType))
        {
            return false;
        }

        if (GetReceiver(invocation) is not { } receiver
            || context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol is not IEventSymbol boundEvent)
        {
            return false;
        }

        raisedEvent = boundEvent;
        invoke = bound;
        return true;
    }

    /// <summary>Returns whether a delegate <c>Invoke</c> has the <c>(object sender, EventArgs args)</c> shape.</summary>
    /// <param name="invoke">The bound delegate invoke method.</param>
    /// <param name="eventArgsType">The resolved <c>System.EventArgs</c> symbol.</param>
    /// <returns><see langword="true"/> when the delegate is an event handler shape.</returns>
    private static bool IsEventArgsShape(IMethodSymbol invoke, INamedTypeSymbol eventArgsType)
    {
        var parameters = invoke.Parameters;
        return parameters.Length == 2
            && parameters[0].Type.SpecialType == SpecialType.System_Object
            && IsOrDerivesFrom(parameters[1].Type, eventArgsType);
    }

    /// <summary>Returns whether a type is, or derives from, the target type.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="target">The target base type.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is or inherits <paramref name="target"/>.</returns>
    private static bool IsOrDerivesFrom(ITypeSymbol type, INamedTypeSymbol target)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads the invoked member's simple name without binding.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns>The member name, or <see langword="null"/> when the call is not a member access.</returns>
    private static string? InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Finds the expression the event delegate is read from.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns>The receiver expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? GetReceiver(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax memberAccess => memberAccess.Expression,
        MemberBindingExpressionSyntax => invocation.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>()?.Expression,
        _ => null,
    };

    /// <summary>Returns whether an expression is a syntactic null literal, seeing through <c>null!</c> and parentheses.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns><see langword="true"/> when the expression is written as null.</returns>
    private static bool IsNullLiteral(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } => true,
        ParenthesizedExpressionSyntax parenthesized => IsNullLiteral(parenthesized.Expression),
        PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed => IsNullLiteral(suppressed.Operand),
        _ => false,
    };
}
