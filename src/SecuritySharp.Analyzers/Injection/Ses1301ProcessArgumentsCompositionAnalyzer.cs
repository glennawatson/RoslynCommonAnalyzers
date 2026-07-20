// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a non-constant string composed into a process command line (SES1301). The rule reports two
/// local shapes: an assignment (or object-initializer member) of <c>System.Diagnostics.ProcessStartInfo.Arguments</c>,
/// and the <c>arguments</c> string of a <c>System.Diagnostics.Process.Start(string fileName, string arguments)</c>
/// call. In both, the value is reported only when it is a composition — an interpolated string with at
/// least one interpolation, or a <c>+</c> concatenation — that is not a compile-time constant
/// (<see cref="SemanticModel.GetConstantValue(SyntaxNode, System.Threading.CancellationToken)"/> decides
/// this precisely, so a fully constant <c>Arguments</c> string is left alone). The suggested fix is to add
/// each argument to <c>ArgumentList</c>, which escapes each argument for the platform; the rule is resolved
/// once per compilation by probing <c>ProcessStartInfo</c> and confirming it exposes <c>ArgumentList</c>
/// (a .NET Core 2.1+ member). On a target framework without it (netstandard2.0, .NET Framework) nothing is
/// registered, so a project that cannot use <c>ArgumentList</c> pays nothing and never receives a diagnostic
/// it cannot act on.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1301ProcessArgumentsCompositionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the type whose <c>Arguments</c> assignment is inspected.</summary>
    private const string ProcessStartInfoMetadataName = "System.Diagnostics.ProcessStartInfo";

    /// <summary>The metadata name of the type whose <c>Start(string, string)</c> call is inspected.</summary>
    private const string ProcessMetadataName = "System.Diagnostics.Process";

    /// <summary>The safer collection property the rule gates on, to keep its suggestion actionable.</summary>
    private const string ArgumentListPropertyName = "ArgumentList";

    /// <summary>The name of the <c>ProcessStartInfo.Arguments</c> property whose assignment is inspected.</summary>
    private const string ArgumentsPropertyName = "Arguments";

    /// <summary>The name of the <c>arguments</c> parameter on the guarded <c>Process.Start</c> overload.</summary>
    private const string ArgumentsParameterName = "arguments";

    /// <summary>The name of the <c>Process.Start</c> method whose arguments string is inspected.</summary>
    private const string StartMethodName = "Start";

    /// <summary>The sink name reported for a <c>ProcessStartInfo.Arguments</c> assignment.</summary>
    private const string ProcessStartInfoArgumentsDisplayName = "ProcessStartInfo.Arguments";

    /// <summary>The sink name reported for a <c>Process.Start</c> arguments string.</summary>
    private const string ProcessStartArgumentsDisplayName = "Process.Start arguments";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.ProcessArgumentsComposition);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // Gate on ProcessStartInfo.ArgumentList: without it the 'use ArgumentList' suggestion is not
            // actionable, so the rule stays silent on netstandard2.0 / .NET Framework.
            var processStartInfoType = start.Compilation.GetTypeByMetadataName(ProcessStartInfoMetadataName);
            if (processStartInfoType is null || processStartInfoType.GetMembers(ArgumentListPropertyName).Length == 0)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, processStartInfoType), SyntaxKind.SimpleAssignmentExpression);

            var processType = start.Compilation.GetTypeByMetadataName(ProcessMetadataName);
            if (processType is not null)
            {
                start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, processType), SyntaxKind.InvocationExpression);
            }
        });
    }

    /// <summary>Reports SES1301 for a <c>ProcessStartInfo.Arguments</c> assignment given a non-constant composition.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="processStartInfoType">The gated <c>ProcessStartInfo</c> type resolved for the compilation.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol processStartInfoType)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: 'x.Arguments = <interp|concat>' or object-initializer 'Arguments = <interp|concat>'.
        if (!IsArgumentsTarget(assignment.Left) || !IsCompositionShape(assignment.Right))
        {
            return;
        }

        // Bind the target: report only when it truly resolves to ProcessStartInfo.Arguments, so a
        // same-named property on an unrelated type is never flagged.
        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol { Name: ArgumentsPropertyName } property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, processStartInfoType))
        {
            return;
        }

        // Precise: a fully-constant composition ("a" + "b", $"{constA}") carries no injectable value.
        if (context.SemanticModel.GetConstantValue(assignment.Right, context.CancellationToken).HasValue)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.ProcessArgumentsComposition,
            assignment.Right.SyntaxTree,
            assignment.Right.Span,
            ProcessStartInfoArgumentsDisplayName));
    }

    /// <summary>Reports SES1301 for a <c>Process.Start(fileName, arguments)</c> call whose arguments string is a non-constant composition.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="processType">The gated <c>Process</c> type resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol processType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.Start(...)' call with at least two arguments whose arguments slot is a composition.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: StartMethodName }
            || invocation.ArgumentList.Arguments.Count < 2
            || GetArgumentsArgument(invocation.ArgumentList) is not { } argumentsExpression
            || !IsCompositionShape(argumentsExpression))
        {
            return;
        }

        // Bind the call: only the Process.Start(string fileName, string arguments) overload qualifies.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: StartMethodName } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, processType)
            || !HasStringArgumentsParameter(method))
        {
            return;
        }

        // Precise: a fully-constant composition carries no injectable value.
        if (context.SemanticModel.GetConstantValue(argumentsExpression, context.CancellationToken).HasValue)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.ProcessArgumentsComposition,
            argumentsExpression.SyntaxTree,
            argumentsExpression.Span,
            ProcessStartArgumentsDisplayName));
    }

    /// <summary>Returns the <c>arguments</c> argument, honouring an explicit <c>arguments:</c> name.</summary>
    /// <param name="argumentList">The invocation's argument list (already known to hold at least two arguments).</param>
    /// <returns>The arguments expression, or <see langword="null"/> when it cannot be identified positionally.</returns>
    private static ExpressionSyntax? GetArgumentsArgument(ArgumentListSyntax argumentList)
    {
        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { Name.Identifier.ValueText: ArgumentsParameterName })
            {
                return arguments[i].Expression;
            }
        }

        // 'arguments' is the second parameter of Process.Start(string, string), so a positional second
        // argument (no earlier argument being named, which C# guarantees) is the arguments string.
        return arguments[1].NameColon is null ? arguments[1].Expression : null;
    }

    /// <summary>Returns whether an assignment target names the <c>Arguments</c> member.</summary>
    /// <param name="left">The assignment's left-hand side.</param>
    /// <returns><see langword="true"/> for a <c>.Arguments</c> or bare <c>Arguments</c> target.</returns>
    private static bool IsArgumentsTarget(ExpressionSyntax left)
        => left switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: ArgumentsPropertyName } => true,
            IdentifierNameSyntax { Identifier.ValueText: ArgumentsPropertyName } => true,
            _ => false,
        };

    /// <summary>Returns whether an expression is a command-line composition shape (interpolation or <c>+</c> concatenation).</summary>
    /// <param name="expression">The candidate value expression.</param>
    /// <returns><see langword="true"/> for an interpolated string with an interpolation, or an add expression.</returns>
    private static bool IsCompositionShape(ExpressionSyntax expression)
        => expression switch
        {
            InterpolatedStringExpressionSyntax interpolated => HasInterpolation(interpolated),
            BinaryExpressionSyntax binary => binary.IsKind(SyntaxKind.AddExpression),
            _ => false,
        };

    /// <summary>Returns whether an interpolated string contains at least one interpolation hole.</summary>
    /// <param name="interpolated">The interpolated string expression.</param>
    /// <returns><see langword="true"/> when at least one content item is an interpolation.</returns>
    private static bool HasInterpolation(InterpolatedStringExpressionSyntax interpolated)
    {
        var contents = interpolated.Contents;
        for (var i = 0; i < contents.Count; i++)
        {
            if (contents[i].IsKind(SyntaxKind.Interpolation))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a bound method is the <c>Start(string fileName, string arguments)</c> overload.</summary>
    /// <param name="method">The bound <c>Start</c> method.</param>
    /// <returns><see langword="true"/> when its second parameter is a <c>string</c> named <c>arguments</c>.</returns>
    private static bool HasStringArgumentsParameter(IMethodSymbol method)
    {
        var parameters = method.Parameters;
        if (parameters.Length < 2)
        {
            return false;
        }

        var second = parameters[1];
        return second.Type.SpecialType == SpecialType.System_String
            && string.Equals(second.Name, ArgumentsParameterName, StringComparison.Ordinal);
    }
}
