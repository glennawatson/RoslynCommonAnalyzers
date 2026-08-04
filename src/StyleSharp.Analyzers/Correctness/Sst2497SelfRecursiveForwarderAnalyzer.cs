// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an expression-bodied method or local function whose body calls its own name and hands every
/// parameter straight back in its own position (SST2497). Such a call binds to the member being
/// declared, so it recurses with identical arguments until the stack overflows.
/// </summary>
/// <remarks>
/// The shape is found syntactically and then confirmed against the semantic model: the invocation has to
/// bind to the very symbol the declaration declares. That confirmation is what makes the report safe —
/// an exact signature match beats any overload needing a conversion, but rather than reason about
/// overload resolution the rule simply asks which member the compiler picked.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2497SelfRecursiveForwarderAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.SelfRecursiveForwarder);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeLocalFunction, SyntaxKind.LocalFunctionStatement);
    }

    /// <summary>Analyzes an expression-bodied method declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        Analyze(context, method, method.Identifier, method.ParameterList, method.ExpressionBody);
    }

    /// <summary>Analyzes an expression-bodied local function.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context)
    {
        var localFunction = (LocalFunctionStatementSyntax)context.Node;
        Analyze(context, localFunction, localFunction.Identifier, localFunction.ParameterList, localFunction.ExpressionBody);
    }

    /// <summary>Reports a body that forwards every parameter to the member being declared.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="declaration">The member declaration.</param>
    /// <param name="identifier">The declared name.</param>
    /// <param name="parameters">The declared parameter list.</param>
    /// <param name="expressionBody">The expression body, when the member has one.</param>
    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        SyntaxNode declaration,
        SyntaxToken identifier,
        ParameterListSyntax parameters,
        ArrowExpressionClauseSyntax? expressionBody)
    {
        if (expressionBody?.Expression is not InvocationExpressionSyntax invocation
            || !IsSelfNamedCall(invocation.Expression, identifier.ValueText)
            || !ForwardsEveryParameter(invocation.ArgumentList, parameters))
        {
            return;
        }

        var declared = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
        if (declared is null
            || !SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol, declared))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(CorrectnessRules.SelfRecursiveForwarder, invocation.GetLocation(), identifier.ValueText));
    }

    /// <summary>Returns whether an invocation targets the declared name directly or through <c>this</c>.</summary>
    /// <param name="callee">The invoked expression.</param>
    /// <param name="name">The declared name.</param>
    /// <returns><see langword="true"/> when the call names the member being declared.</returns>
    private static bool IsSelfNamedCall(ExpressionSyntax callee, string name)
        => callee switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == name,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax member } => member.Identifier.ValueText == name,
            _ => false
        };

    /// <summary>Returns whether every argument is the parameter declared in that same position.</summary>
    /// <param name="arguments">The argument list of the invocation.</param>
    /// <param name="parameters">The declared parameter list.</param>
    /// <returns><see langword="true"/> when the call passes its own parameters through unchanged.</returns>
    private static bool ForwardsEveryParameter(ArgumentListSyntax arguments, ParameterListSyntax parameters)
    {
        if (arguments.Arguments.Count != parameters.Parameters.Count)
        {
            return false;
        }

        for (var i = 0; i < arguments.Arguments.Count; i++)
        {
            // A named argument may sit in any position, so position alone no longer proves what is passed.
            if (arguments.Arguments[i] is not { NameColon: null, Expression: IdentifierNameSyntax argument }
                || argument.Identifier.ValueText != parameters.Parameters[i].Identifier.ValueText)
            {
                return false;
            }
        }

        return true;
    }
}
