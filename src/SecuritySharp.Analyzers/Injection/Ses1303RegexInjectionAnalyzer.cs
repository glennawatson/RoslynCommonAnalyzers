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
/// <c>Regex</c> type is probed once per compilation and, when absent, nothing is registered. There is no
/// code fix: wrapping the data in <c>Regex.Escape</c> changes matching semantics, so the rewrite is left
/// to the author.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1303RegexInjectionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the regular-expression type whose pattern argument is guarded.</summary>
    private const string RegexMetadataName = "System.Text.RegularExpressions.Regex";

    /// <summary>The simple type name used to prefilter object-creation nodes syntactically.</summary>
    private const string RegexTypeName = "Regex";

    /// <summary>The name of the pattern parameter on every guarded constructor and static overload.</summary>
    private const string PatternParameterName = "pattern";

    /// <summary>The message sink label used for a constructor call.</summary>
    private const string ConstructorSink = "new Regex";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.RegexInjection);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var regexType = start.Compilation.GetTypeByMetadataName(RegexMetadataName);
            if (regexType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeObjectCreation(nodeContext, regexType), SyntaxKind.ObjectCreationExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, regexType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1303 for a <c>new Regex(pattern, ...)</c> whose pattern argument is non-constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="regexType">The gated <c>Regex</c> type resolved for the compilation.</param>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol regexType)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // Syntactic prefilter: a 'new <...>.Regex(...)' with at least one argument.
        if (objectCreation.ArgumentList is not { Arguments.Count: > 0 } argumentList
            || !IsRegexTypeName(objectCreation.Type))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(objectCreation, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, regexType))
        {
            return;
        }

        ReportWhenPatternNonConstant(context, argumentList, constructor, ConstructorSink);
    }

    /// <summary>Reports SES1303 for a static <c>Regex</c> call whose pattern argument is non-constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="regexType">The gated <c>Regex</c> type resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol regexType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member call to one of the guarded static methods carrying at least the
        // input and pattern arguments. The static overloads always take '(input, pattern, ...)'.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: { } methodName }
            || !IsGuardedStaticMethodName(methodName)
            || invocation.ArgumentList.Arguments.Count < 2)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { IsStatic: true } method
            || !IsGuardedStaticMethodName(method.Name)
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, regexType))
        {
            return;
        }

        ReportWhenPatternNonConstant(context, invocation.ArgumentList, method, "Regex." + method.Name);
    }

    /// <summary>Reports SES1303 when the method's <c>pattern</c> argument is not a compile-time constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="argumentList">The call's argument list.</param>
    /// <param name="method">The bound constructor or static method.</param>
    /// <param name="sink">The message label identifying the call.</param>
    private static void ReportWhenPatternNonConstant(SyntaxNodeAnalysisContext context, ArgumentListSyntax argumentList, IMethodSymbol method, string sink)
    {
        if (GetPatternArgument(argumentList, method) is not { } patternExpression
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

    /// <summary>Returns the pattern argument expression, honouring an explicit <c>pattern:</c> name.</summary>
    /// <param name="argumentList">The call's argument list.</param>
    /// <param name="method">The bound constructor or static method whose <c>pattern</c> parameter is located.</param>
    /// <returns>The pattern argument expression, or <see langword="null"/> when it cannot be identified.</returns>
    private static ExpressionSyntax? GetPatternArgument(ArgumentListSyntax argumentList, IMethodSymbol method)
    {
        var arguments = argumentList.Arguments;

        // An explicitly named 'pattern:' argument binds regardless of position.
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { Name.Identifier.ValueText: PatternParameterName })
            {
                return arguments[i].Expression;
            }
        }

        // Otherwise the pattern is the positional argument at the parameter's ordinal.
        var parameters = method.Parameters;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Name == PatternParameterName)
            {
                return i < arguments.Count && arguments[i].NameColon is null ? arguments[i].Expression : null;
            }
        }

        return null;
    }

    /// <summary>Returns whether an object-creation type names the <c>Regex</c> type.</summary>
    /// <param name="type">The created type syntax.</param>
    /// <returns><see langword="true"/> when the right-most name is <c>Regex</c>.</returns>
    private static bool IsRegexTypeName(TypeSyntax type)
        => type switch
        {
            IdentifierNameSyntax { Identifier.ValueText: RegexTypeName } => true,
            QualifiedNameSyntax { Right.Identifier.ValueText: RegexTypeName } => true,
            _ => false,
        };

    /// <summary>Returns whether a name is one of the guarded static <c>Regex</c> methods.</summary>
    /// <param name="name">The candidate method name.</param>
    /// <returns><see langword="true"/> for <c>IsMatch</c>, <c>Match</c>, <c>Matches</c>, <c>Replace</c>, or <c>Split</c>.</returns>
    private static bool IsGuardedStaticMethodName(string name)
        => name switch
        {
            "IsMatch" or "Match" or "Matches" or "Replace" or "Split" => true,
            _ => false,
        };
}
