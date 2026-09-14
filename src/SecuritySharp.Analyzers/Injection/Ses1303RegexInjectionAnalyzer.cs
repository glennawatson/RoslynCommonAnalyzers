// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a regular-expression pattern built from non-constant data (SES1303). The rule reports the
/// <c>pattern</c> argument of <c>new System.Text.RegularExpressions.Regex(pattern, ...)</c> and of the
/// static <c>Regex.IsMatch</c>, <c>Regex.Match</c>, <c>Regex.Matches</c>, <c>Regex.Replace</c>, and
/// <c>Regex.Split(input, pattern, ...)</c> overloads whenever that argument is not a compile-time
/// constant (<see cref="SemanticModel.GetConstantValue(SyntaxNode, CancellationToken)"/> has no value).
/// A data-derived pattern lets an attacker inject regex metacharacters — alternation, catastrophic
/// backtracking, capture rewriting — so the untrusted text controls the matching grammar rather than
/// only the text being searched. Only the pattern argument is inspected; a regex run over non-constant
/// input with a constant pattern is a separate matching-timeout concern and is not reported here. The
/// <c>Regex</c> type is resolved only after a constructor or call passes the syntax checks. There is no
/// code fix: wrapping the data in <c>Regex.Escape</c> changes matching semantics, so the rewrite is left
/// to the author.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1303RegexInjectionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the pattern parameter on every guarded constructor and static overload.</summary>
    private const string PatternParameterName = "pattern";

    /// <summary>The message sink label used for a constructor call.</summary>
    private const string ConstructorSink = "new Regex";

    /// <summary>The metadata name of the regular-expression type whose pattern argument is guarded.</summary>
    private const string RegexMetadataName = "System.Text.RegularExpressions.Regex";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.RegexInjection);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeActions(
            context,
            static compilation => new LazyMetadataType(compilation, RegexMetadataName),
            new(AnalyzeObjectCreation, [SyntaxKind.ObjectCreationExpression]),
            new(AnalyzeInvocation, [SyntaxKind.InvocationExpression]));
    }

    /// <summary>Reports SES1303 for a <c>new Regex(pattern, ...)</c> whose pattern argument is non-constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="regexTypes">The regex type resolved on demand for this compilation.</param>
    private static void AnalyzeObjectCreation(in SyntaxNodeAnalysisContext context, LazyMetadataType regexTypes)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // Syntactic prefilter: a 'new <...>.Regex(...)' with at least one argument.
        if (!RegexCallSyntax.TryGetCreationArguments(objectCreation, out var argumentList))
        {
            return;
        }

        if (regexTypes.Get() is not { } regexType
            || !RegexCallSyntax.TryBindConstructor(context.SemanticModel, objectCreation, regexType, context.CancellationToken, out var constructor))
        {
            return;
        }

        ReportWhenPatternNonConstant(context, argumentList, constructor, ConstructorSink);
    }

    /// <summary>Reports SES1303 for a static <c>Regex</c> call whose pattern argument is non-constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="regexTypes">The regex type resolved on demand for this compilation.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyMetadataType regexTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member call to one of the guarded static methods carrying at least the
        // input and pattern arguments. The static overloads always take '(input, pattern, ...)'.
        if (!RegexCallSyntax.TryGetStaticCallName(invocation, out var methodName)
            || !RegexCallSyntax.IsPatternMethodName(methodName))
        {
            return;
        }

        if (regexTypes.Get() is not { } regexType
            || !RegexCallSyntax.TryBindStaticMethod(context.SemanticModel, invocation, regexType, context.CancellationToken, out var method)
            || !RegexCallSyntax.IsPatternMethodName(method.Name))
        {
            return;
        }

        ReportWhenPatternNonConstant(context, invocation.ArgumentList, method, $"Regex.{method.Name}");
    }

    /// <summary>Reports SES1303 when the method's <c>pattern</c> argument is not a compile-time constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="argumentList">The call's argument list.</param>
    /// <param name="method">The bound constructor or static method.</param>
    /// <param name="sink">The message label identifying the call.</param>
    private static void ReportWhenPatternNonConstant(in SyntaxNodeAnalysisContext context, ArgumentListSyntax argumentList, IMethodSymbol method, string sink)
    {
        if (ArgumentLookup.FindForParameter(argumentList.Arguments, method, PatternParameterName)?.Expression is not { } patternExpression
            || context.SemanticModel.GetConstantValue(patternExpression, context.CancellationToken).HasValue)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.RegexInjection,
            patternExpression.SyntaxTree,
            patternExpression.Span,
            sink));
    }
}
