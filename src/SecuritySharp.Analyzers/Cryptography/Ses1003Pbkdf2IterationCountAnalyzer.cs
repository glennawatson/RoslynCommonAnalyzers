// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a <c>Rfc2898DeriveBytes.Pbkdf2</c> one-shot call whose <c>iterations</c> argument is a
/// compile-time constant below the configured floor (SES1003). A low iteration count makes an
/// offline attack on a leaked salt and derived key cheap. The rule inspects only the static
/// <c>Pbkdf2</c> surface -- a purely local shape where the count is a direct argument, so no flow
/// analysis is needed -- and reports the argument only when its constant value is strictly less than
/// the floor (default 100000, overridable per project via <c>securitysharp.SES1003.iterations</c> or
/// <c>securitysharp.iterations</c>). A non-constant count, whose value cannot be judged from the
/// source, stays silent. The rule is gated on <c>Rfc2898DeriveBytes</c> resolving in the compilation,
/// so a target framework without it pays nothing and never receives a diagnostic it cannot act on.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1003Pbkdf2IterationCountAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The default iteration floor when no <c>.editorconfig</c> value is set.</summary>
    internal const int DefaultIterationFloor = 100_000;

    /// <summary>The name of the static one-shot key-derivation method whose iteration count is inspected.</summary>
    private const string Pbkdf2MethodName = "Pbkdf2";

    /// <summary>The name of the iteration-count parameter on every <c>Pbkdf2</c> overload.</summary>
    private const string IterationsParameterName = "iterations";

    /// <summary>The zero-based position of the <c>iterations</c> parameter on every <c>Pbkdf2</c> overload.</summary>
    private const int IterationsPosition = 2;

    /// <summary>The metadata name of the type that declares the <c>Pbkdf2</c> one-shot.</summary>
    private const string Rfc2898MetadataName = "System.Security.Cryptography.Rfc2898DeriveBytes";

    /// <summary>The rule-specific floor key.</summary>
    private const string IterationsRuleKey = "securitysharp.SES1003.iterations";

    /// <summary>The project-wide floor key.</summary>
    private const string IterationsGeneralKey = "securitysharp.iterations";

    /// <summary>The smallest floor that means anything: a floor below 1 would pass every count.</summary>
    private const int SmallestFloor = 1;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.Pbkdf2IterationCount);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var rfc2898Type = start.Compilation.GetTypeByMetadataName(Rfc2898MetadataName);
            if (rfc2898Type is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, rfc2898Type), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1003 for a <c>Pbkdf2</c> call whose constant iteration count is below the floor.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="rfc2898Type">The gated <c>Rfc2898DeriveBytes</c> type resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol rfc2898Type)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.Pbkdf2(...)' call carrying at least one argument.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: Pbkdf2MethodName }
            || invocation.ArgumentList.Arguments.Count == 0
            || GetIterationsArgument(invocation.ArgumentList) is not { } iterationsArgument)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: Pbkdf2MethodName, IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, rfc2898Type))
        {
            return;
        }

        // A non-constant count (a config value or a runtime computation) cannot be judged here, so stay silent.
        var constant = context.SemanticModel.GetConstantValue(iterationsArgument, context.CancellationToken);
        if (!constant.HasValue || constant.Value is not int iterations)
        {
            return;
        }

        var floor = ReadIterationFloor(context.Options.AnalyzerConfigOptionsProvider.GetOptions(iterationsArgument.SyntaxTree));
        if (iterations >= floor)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.Pbkdf2IterationCount,
            iterationsArgument.SyntaxTree,
            iterationsArgument.Span,
            iterations.ToString(CultureInfo.InvariantCulture),
            floor.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Returns the iteration-count argument expression, honouring an explicit <c>iterations:</c> name.</summary>
    /// <param name="argumentList">The invocation's argument list.</param>
    /// <returns>The iteration-count argument expression, or <see langword="null"/> when it cannot be identified positionally.</returns>
    private static ExpressionSyntax? GetIterationsArgument(ArgumentListSyntax argumentList)
    {
        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { Name.Identifier.ValueText: IterationsParameterName })
            {
                return arguments[i].Expression;
            }
        }

        // 'iterations' is the third parameter of every Pbkdf2 overload, so a positional third argument
        // is the count -- but only when nothing before it is named, since a name shifts the mapping.
        if (arguments.Count <= IterationsPosition
            || arguments[0].NameColon is not null
            || arguments[1].NameColon is not null
            || arguments[IterationsPosition].NameColon is not null)
        {
            return null;
        }

        return arguments[IterationsPosition].Expression;
    }

    /// <summary>Reads the iteration floor, preferring the rule-specific key over the project-wide key.</summary>
    /// <param name="options">The analyzer config options for the argument's tree.</param>
    /// <returns>The configured floor, or <see cref="DefaultIterationFloor"/> when neither key parses to a sensible value.</returns>
    private static int ReadIterationFloor(AnalyzerConfigOptions options)
    {
        if (options.TryGetValue(IterationsRuleKey, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= SmallestFloor)
        {
            return parsed;
        }

        return options.TryGetValue(IterationsGeneralKey, out value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
            && parsed >= SmallestFloor
            ? parsed
            : DefaultIterationFloor;
    }
}
