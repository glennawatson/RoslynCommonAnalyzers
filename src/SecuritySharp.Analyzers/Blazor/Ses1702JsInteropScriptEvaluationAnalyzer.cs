// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a JavaScript interop call that targets a script-evaluation primitive (SES1702). An
/// <c>IJSRuntime</c> or <c>IJSObjectReference</c> <c>InvokeAsync</c>/<c>InvokeVoidAsync</c> call names the
/// function to run by its identifier string. When that constant identifier is <c>eval</c>, <c>Function</c>,
/// <c>document.write</c>, or <c>document.writeln</c>, every forwarded argument becomes executable script, so
/// interop turns into a script-injection channel. <c>setTimeout</c> and <c>setInterval</c> are the same
/// hazard, but only when the argument after the identifier is a string body the browser compiles; a real
/// function reference is safe, so they are reported only in the string-body case. The invoked method is bound
/// and its receiver confirmed to be (or implement) <c>IJSRuntime</c>/<c>IJSObjectReference</c>, so a
/// same-named method on an unrelated type is ignored, and a non-constant identifier is never judged here. The
/// whole rule is gated on <c>Microsoft.JSInterop.IJSRuntime</c> resolving, so a non-Blazor project registers
/// nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1702JsInteropScriptEvaluationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the JS runtime interface the rule gates on.</summary>
    private const string JsRuntimeMetadataName = "Microsoft.JSInterop.IJSRuntime";

    /// <summary>The metadata name of the JS object-reference interface that shares the interop surface.</summary>
    private const string JsObjectReferenceMetadataName = "Microsoft.JSInterop.IJSObjectReference";

    /// <summary>The generic interop method that returns a value.</summary>
    private const string InvokeAsyncMethodName = "InvokeAsync";

    /// <summary>The void interop convenience method.</summary>
    private const string InvokeVoidAsyncMethodName = "InvokeVoidAsync";

    /// <summary>The zero-based position of the body argument that follows the function identifier.</summary>
    private const int BodyArgumentPosition = 1;

    /// <summary>Identifiers whose every forwarded argument is evaluated as script, regardless of the next argument.</summary>
    private static readonly string[] AlwaysEvaluatingIdentifiers =
    [
        "eval",
        "Function",
        "document.write",
        "document.writeln"
    ];

    /// <summary>Identifiers that evaluate a script only when their first argument is a string body.</summary>
    private static readonly string[] StringBodyIdentifiers =
    [
        "setTimeout",
        "setInterval"
    ];

    /// <summary>How a constant identifier maps onto the eval-class hazard.</summary>
    private enum IdentifierClass
    {
        /// <summary>Not an eval-class identifier.</summary>
        None,

        /// <summary>Always evaluates forwarded arguments as script.</summary>
        AlwaysEvaluating,

        /// <summary>Evaluates a script only when the next argument is a string body.</summary>
        StringBody,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.JsInteropScriptEvaluation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var jsRuntime = start.Compilation.GetTypeByMetadataName(JsRuntimeMetadataName);
            if (jsRuntime is null)
            {
                return;
            }

            var jsObjectReference = start.Compilation.GetTypeByMetadataName(JsObjectReferenceMetadataName);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, jsRuntime, jsObjectReference), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1702 for an interop call whose constant identifier is an eval-class primitive.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="jsRuntime">The gated <c>IJSRuntime</c> type resolved for the compilation.</param>
    /// <param name="jsObjectReference">The optional <c>IJSObjectReference</c> type; <see langword="null"/> when absent.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol jsRuntime, INamedTypeSymbol? jsObjectReference)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.InvokeAsync(...)' / '.InvokeVoidAsync(...)' call with an identifier argument.
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || invocation.ArgumentList.Arguments.Count == 0
            || !IsInvokeMethodName(memberAccess.Name.Identifier.ValueText))
        {
            return;
        }

        var identifierArgument = invocation.ArgumentList.Arguments[0].Expression;
        if (!IsReportableEvalIdentifier(context, invocation.ArgumentList, identifierArgument, out var identifier)
            || !ResolvesToInteropInvoke(context, invocation, jsRuntime, jsObjectReference))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.JsInteropScriptEvaluation,
            identifierArgument.SyntaxTree,
            identifierArgument.Span,
            identifier));
    }

    /// <summary>Returns whether the identifier argument is a constant eval-class value that must be reported.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="argumentList">The interop call's argument list.</param>
    /// <param name="identifierArgument">The function-identifier argument expression.</param>
    /// <param name="identifier">The matched constant identifier, when reportable.</param>
    /// <returns><see langword="true"/> when the identifier is eval-class and (for string-body identifiers) is followed by a string body.</returns>
    private static bool IsReportableEvalIdentifier(SyntaxNodeAnalysisContext context, ArgumentListSyntax argumentList, ExpressionSyntax identifierArgument, out string identifier)
    {
        identifier = string.Empty;

        // The identifier must be a constant string; a runtime-computed identifier cannot be judged here.
        var constant = context.SemanticModel.GetConstantValue(identifierArgument, context.CancellationToken);
        if (!constant.HasValue || constant.Value is not string value)
        {
            return false;
        }

        var classification = ClassifyIdentifier(value);
        if (classification == IdentifierClass.None
            || (classification == IdentifierClass.StringBody && !NextArgumentIsString(context, argumentList)))
        {
            return false;
        }

        identifier = value;
        return true;
    }

    /// <summary>Binds the call and returns whether it is an interop invoke on <c>IJSRuntime</c>/<c>IJSObjectReference</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The candidate interop invocation.</param>
    /// <param name="jsRuntime">The gated <c>IJSRuntime</c> type.</param>
    /// <param name="jsObjectReference">The optional <c>IJSObjectReference</c> type.</param>
    /// <returns><see langword="true"/> when the invocation resolves to an interop invoke method on one of the interfaces.</returns>
    private static bool ResolvesToInteropInvoke(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, INamedTypeSymbol jsRuntime, INamedTypeSymbol? jsObjectReference)
        => context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
            && TargetsJsInterop(method, jsRuntime, jsObjectReference);

    /// <summary>Returns whether a name is one of the interop invoke methods.</summary>
    /// <param name="name">The candidate method name.</param>
    /// <returns><see langword="true"/> when the name is <c>InvokeAsync</c> or <c>InvokeVoidAsync</c>.</returns>
    private static bool IsInvokeMethodName(string name)
        => string.Equals(name, InvokeAsyncMethodName, StringComparison.Ordinal)
            || string.Equals(name, InvokeVoidAsyncMethodName, StringComparison.Ordinal);

    /// <summary>Classifies a constant identifier into the eval-class hazard it represents.</summary>
    /// <param name="identifier">The constant function-identifier string.</param>
    /// <returns>The identifier's eval-class category.</returns>
    private static IdentifierClass ClassifyIdentifier(string identifier)
    {
        for (var i = 0; i < AlwaysEvaluatingIdentifiers.Length; i++)
        {
            if (string.Equals(AlwaysEvaluatingIdentifiers[i], identifier, StringComparison.Ordinal))
            {
                return IdentifierClass.AlwaysEvaluating;
            }
        }

        for (var i = 0; i < StringBodyIdentifiers.Length; i++)
        {
            if (string.Equals(StringBodyIdentifiers[i], identifier, StringComparison.Ordinal))
            {
                return IdentifierClass.StringBody;
            }
        }

        return IdentifierClass.None;
    }

    /// <summary>Returns whether the argument after the identifier is typed as <see cref="string"/>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="argumentList">The interop call's argument list.</param>
    /// <returns><see langword="true"/> when a body argument exists and its type is <c>string</c>.</returns>
    private static bool NextArgumentIsString(SyntaxNodeAnalysisContext context, ArgumentListSyntax argumentList)
        => argumentList.Arguments.Count > BodyArgumentPosition
            && context.SemanticModel.GetTypeInfo(argumentList.Arguments[BodyArgumentPosition].Expression, context.CancellationToken).Type?.SpecialType == SpecialType.System_String;

    /// <summary>Returns whether a bound interop method is invoked on <c>IJSRuntime</c> or <c>IJSObjectReference</c>.</summary>
    /// <param name="method">The bound invoke method (an interface member or a reduced extension method).</param>
    /// <param name="jsRuntime">The gated <c>IJSRuntime</c> type.</param>
    /// <param name="jsObjectReference">The optional <c>IJSObjectReference</c> type.</param>
    /// <returns><see langword="true"/> when the receiver is, or implements, one of the interop interfaces.</returns>
    private static bool TargetsJsInterop(IMethodSymbol method, INamedTypeSymbol jsRuntime, INamedTypeSymbol? jsObjectReference)
    {
        // Both the interface's own InvokeAsync and the reduced InvokeVoidAsync extension are invoked on a value
        // receiver, so ReceiverType is always present here.
        var receiver = method.ReceiverType!;
        return IsOrImplements(receiver, jsRuntime)
            || (jsObjectReference is not null && IsOrImplements(receiver, jsObjectReference));
    }

    /// <summary>Returns whether a type is, or implements, the given interface.</summary>
    /// <param name="type">The receiver type of the interop call.</param>
    /// <param name="interfaceType">The interop interface to match.</param>
    /// <returns><see langword="true"/> when the type is the interface or implements it.</returns>
    private static bool IsOrImplements(ITypeSymbol type, INamedTypeSymbol interfaceType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, interfaceType))
        {
            return true;
        }

        foreach (var implemented in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(implemented, interfaceType))
            {
                return true;
            }
        }

        return false;
    }
}
