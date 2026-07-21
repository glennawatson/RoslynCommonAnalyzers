// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a null guard applied to a parameter whose signature says null is allowed (SST2492): a
/// hand-written <c>if (p is null) throw new ArgumentNullException(...)</c> or an
/// <c>ArgumentNullException.ThrowIfNull(p)</c>, where <c>p</c> is a nullable-annotated reference
/// parameter or an optional parameter that defaults to null. The guard and the signature disagree.
/// </summary>
/// <remarks>
/// The clean path is syntactic: the hand-written form is matched with the shared guard patterns and the
/// helper form is matched by name and shape, both before any binding. Only a matched guard binds — the
/// checked expression to a parameter symbol, then the parameter's nullability contract.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2492GuardOnNullableParameterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the runtime null-throw helper.</summary>
    private const string ThrowIfNullMethodName = "ThrowIfNull";

    /// <summary>The message reason for a nullable-annotated parameter.</summary>
    private const string AnnotatedReason = "is declared nullable";

    /// <summary>The message reason for an optional parameter defaulting to null.</summary>
    private const string OptionalNullReason = "defaults to null";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.GuardOnNullableParameter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeIf, SyntaxKind.IfStatement);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports a hand-written null-throw guard applied to a null-permitting parameter.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeIf(SyntaxNodeAnalysisContext context)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (!ThrowGuardPatterns.TryMatchArgumentNull(ifStatement, out var checkedExpression))
        {
            return;
        }

        Report(context, checkedExpression!, ifStatement.GetLocation());
    }

    /// <summary>Reports an <c>ArgumentNullException.ThrowIfNull(p)</c> applied to a null-permitting parameter.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: ThrowIfNullMethodName } access
            || !IsArgumentNullExceptionReceiver(access.Expression)
            || invocation.ArgumentList.Arguments is not [{ Expression: { } argument }])
        {
            return;
        }

        Report(context, argument, invocation.GetLocation());
    }

    /// <summary>Binds the guarded expression to a parameter and reports when its contract permits null.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="guardedExpression">The expression the guard rejects when null.</param>
    /// <param name="location">The location to report.</param>
    private static void Report(SyntaxNodeAnalysisContext context, ExpressionSyntax guardedExpression, Location location)
    {
        if (guardedExpression is not IdentifierNameSyntax identifier
            || context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol is not IParameterSymbol parameter
            || NullContractReason(parameter) is not { } reason)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.GuardOnNullableParameter,
            location,
            parameter.Name,
            reason));
    }

    /// <summary>Returns why a parameter's contract permits null, or <see langword="null"/> when it does not.</summary>
    /// <param name="parameter">The guarded parameter.</param>
    /// <returns>The reason string, or <see langword="null"/> when the signature forbids null.</returns>
    private static string? NullContractReason(IParameterSymbol parameter)
    {
        var type = parameter.Type;
        if ((type.IsReferenceType || type.TypeKind == TypeKind.TypeParameter)
            && parameter.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return AnnotatedReason;
        }

        return parameter is { IsOptional: true, HasExplicitDefaultValue: true, ExplicitDefaultValue: null }
            ? OptionalNullReason
            : null;
    }

    /// <summary>Returns whether a member-access receiver names <c>ArgumentNullException</c>.</summary>
    /// <param name="receiver">The receiver expression.</param>
    /// <returns><see langword="true"/> for the simple or qualified <c>ArgumentNullException</c> name.</returns>
    private static bool IsArgumentNullExceptionReceiver(ExpressionSyntax receiver) => receiver switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text == nameof(ArgumentNullException),
        MemberAccessExpressionSyntax qualified => qualified.Name.Identifier.Text == nameof(ArgumentNullException),
        _ => false,
    };
}
