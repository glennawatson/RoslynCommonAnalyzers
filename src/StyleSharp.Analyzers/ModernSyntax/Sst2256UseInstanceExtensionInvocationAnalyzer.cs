// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an extension method invoked in static form — <c>Type.Extension(receiver, args)</c> — that reads
/// better in instance form <c>receiver.Extension(args)</c> (SST2256). The rewrite is only offered when the
/// first argument binds to the extension's <c>this</c> parameter and the instance form provably resolves to
/// the same method.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2256UseInstanceExtensionInvocationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseInstanceExtensionInvocation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    /// <summary>Builds the instance-form invocation for a static-form extension call, when the rewrite is safe.</summary>
    /// <param name="invocation">The static-form invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="instanceForm">The equivalent instance-form invocation, without outer trivia.</param>
    /// <param name="methodName">The extension method's name, for the diagnostic message.</param>
    /// <returns><see langword="true"/> when a safe instance-form rewrite exists.</returns>
    internal static bool TryBuildInstanceForm(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out InvocationExpressionSyntax instanceForm,
        out string methodName)
    {
        instanceForm = null!;
        methodName = string.Empty;

        if (!TryGetStaticExtensionCall(invocation, model, cancellationToken, out var access, out var method))
        {
            return false;
        }

        var candidate = BuildInstanceForm(access, invocation.ArgumentList.Arguments);
        if (!RebindsToSameMethod(invocation, candidate, method, model))
        {
            return false;
        }

        instanceForm = candidate;
        methodName = method.Name;
        return true;
    }

    /// <summary>Recognizes a static-form extension call whose first argument is the receiver.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="access">The static-form member access (<c>Type.Method</c>).</param>
    /// <param name="method">The unreduced extension method.</param>
    /// <returns><see langword="true"/> when the invocation is a static-form extension call.</returns>
    private static bool TryGetStaticExtensionCall(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellationToken,
        out MemberAccessExpressionSyntax access,
        out IMethodSymbol method)
    {
        access = null!;
        method = null!;

        if (invocation.Expression is not MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } memberAccess)
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0 || arguments[0].NameColon is not null || arguments[0].RefKindKeyword.RawKind != 0)
        {
            return false;
        }

        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol { IsExtensionMethod: true, MethodKind: MethodKind.Ordinary, ReducedFrom: null } candidate
            || candidate.Parameters.Length != arguments.Count)
        {
            return false;
        }

        access = memberAccess;
        method = candidate;
        return true;
    }

    /// <summary>Returns whether the candidate instance form reduces to the same extension method.</summary>
    /// <param name="invocation">The original static-form invocation.</param>
    /// <param name="candidate">The candidate instance-form invocation.</param>
    /// <param name="method">The unreduced extension method the static form resolved to.</param>
    /// <param name="model">The semantic model.</param>
    /// <returns><see langword="true"/> when the instance form binds to the same method.</returns>
    private static bool RebindsToSameMethod(
        InvocationExpressionSyntax invocation,
        InvocationExpressionSyntax candidate,
        IMethodSymbol method,
        SemanticModel model)
    {
        var bound = model.GetSpeculativeSymbolInfo(invocation.SpanStart, candidate, SpeculativeBindingOption.BindAsExpression).Symbol;
        return bound is IMethodSymbol { ReducedFrom: { } reducedFrom } && SymbolEqualityComparer.Default.Equals(reducedFrom, method);
    }

    /// <summary>Reports a static-form extension call that could be an instance-form call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!TryBuildInstanceForm(invocation, context.SemanticModel, context.CancellationToken, out _, out var methodName))
        {
            return;
        }

        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.UseInstanceExtensionInvocation, access.Name.GetLocation(), methodName));
    }

    /// <summary>Builds <c>receiver.Method(remainingArgs)</c> from a static-form call.</summary>
    /// <param name="access">The static-form member access (<c>Type.Method</c>).</param>
    /// <param name="arguments">The full argument list; the first becomes the receiver.</param>
    /// <returns>The instance-form invocation.</returns>
    private static InvocationExpressionSyntax BuildInstanceForm(MemberAccessExpressionSyntax access, in SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        var receiver = AsReceiver(arguments[0].Expression.WithoutTrivia());
        var memberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, access.Name.WithoutTrivia());

        var remaining = SyntaxFactory.SeparatedList<ArgumentSyntax>();
        for (var i = 1; i < arguments.Count; i++)
        {
            remaining = remaining.Add(arguments[i]);
        }

        return SyntaxFactory.InvocationExpression(memberAccess, SyntaxFactory.ArgumentList(remaining));
    }

    /// <summary>Wraps a would-be receiver in parentheses unless it is already a primary expression.</summary>
    /// <param name="expression">The receiver expression.</param>
    /// <returns>The receiver, parenthesized when its precedence requires it.</returns>
    private static ExpressionSyntax AsReceiver(ExpressionSyntax expression)
        => PrimaryExpressionClassification.IsPrimary(expression) ? expression : SyntaxFactory.ParenthesizedExpression(expression);
}
