// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a security-check method that fails open (SES1508). The rule reports a <c>catch</c> clause when
/// all of the following hold: the clause is directly inside a method (or local function) whose name begins
/// with <c>Validate</c>, <c>Verify</c>, <c>Authenticate</c>, <c>Authorize</c>, <c>Check</c>, <c>IsValid</c>,
/// <c>IsAuthentic</c>, or <c>Ensure</c> and that returns <c>bool</c>, <c>Task&lt;bool&gt;</c>, or
/// <c>ValueTask&lt;bool&gt;</c>; the clause swallows a broad exception (a bare <c>catch</c> or
/// <c>catch (System.Exception)</c>) or a security-relevant one (a cryptographic exception, an authentication
/// exception, or a type whose name ends in <c>SecurityTokenException</c>); and the clause's only effect is to
/// return success -- a lone <c>return true;</c> (or a <c>FromResult(true)</c>), or an empty body that falls
/// through to a trailing <c>return true;</c>. Swallowing the error and returning success means an attacker who
/// can force the exception is validated or authenticated. The rule is purely local to the single method and
/// never traces values across calls; its clean path is a syntax-only prefilter on the enclosing method's name
/// and return type, so a <c>catch</c> outside a security-check method costs nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1508FailOpenValidationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The suffix that identifies a security-token exception type by its unqualified name.</summary>
    private const string SecurityTokenExceptionSuffix = "SecurityTokenException";

    /// <summary>The name of the <c>Task.FromResult</c>/<c>ValueTask.FromResult</c> success factory.</summary>
    private const string FromResultMethodName = "FromResult";

    /// <summary>The method-name prefixes that mark a method as a security check.</summary>
    private static readonly string[] SecurityCheckPrefixes =
    [
        "Validate",
        "Verify",
        "Authenticate",
        "Authorize",
        "Check",
        "IsValid",
        "IsAuthentic",
        "Ensure"
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.FailOpenValidation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var knownExceptions = new KnownSecurityExceptions(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeCatchClause(nodeContext, knownExceptions), SyntaxKind.CatchClause);
        });
    }

    /// <summary>Reports SES1508 for a <c>catch</c> that swallows a broad or security-relevant exception and returns success.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="knownExceptions">The broad and security-relevant exception types resolved for the compilation.</param>
    private static void AnalyzeCatchClause(SyntaxNodeAnalysisContext context, KnownSecurityExceptions knownExceptions)
    {
        var catchClause = (CatchClauseSyntax)context.Node;

        // Syntax-only prefilter: only a catch that both sits in a bool-returning security-check method and
        // whose sole effect is to return success can fail open. Neither probe touches the semantic model.
        if (GetEnclosingSecurityCheckName(catchClause) is not { } methodName
            || !CatchReturnsSuccess(catchClause))
        {
            return;
        }

        // Rare path: confirm the swallowed exception is broad or security-relevant.
        if (!CatchesBroadOrSecurityException(catchClause, context.SemanticModel, knownExceptions, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.FailOpenValidation,
            catchClause.SyntaxTree,
            catchClause.CatchKeyword.Span,
            methodName));
    }

    /// <summary>Returns the name of the enclosing security-check method, or <see langword="null"/> when the catch is not in one.</summary>
    /// <param name="catchClause">The catch clause under inspection.</param>
    /// <returns>The enclosing method's name when it is a bool-returning security check; otherwise <see langword="null"/>.</returns>
    private static string? GetEnclosingSecurityCheckName(CatchClauseSyntax catchClause)
    {
        for (var node = catchClause.Parent; node is not null; node = node.Parent)
        {
            if (node is MethodDeclarationSyntax method)
            {
                return IsSecurityCheck(method.Identifier.ValueText, method.ReturnType) ? method.Identifier.ValueText : null;
            }

            if (node is LocalFunctionStatementSyntax localFunction)
            {
                return IsSecurityCheck(localFunction.Identifier.ValueText, localFunction.ReturnType) ? localFunction.Identifier.ValueText : null;
            }

            // A lambda, anonymous method, constructor/operator/destructor, or accessor is the nearest function
            // boundary: a 'return' in the catch leaves that member, not the surrounding security-check method.
            if (node is AnonymousFunctionExpressionSyntax or BaseMethodDeclarationSyntax or AccessorDeclarationSyntax)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Returns whether a method's name and return type mark it as a boolean security check.</summary>
    /// <param name="methodName">The method name.</param>
    /// <param name="returnType">The method return type.</param>
    /// <returns><see langword="true"/> when the method is a bool-returning security check.</returns>
    private static bool IsSecurityCheck(string methodName, TypeSyntax returnType)
        => HasSecurityCheckPrefix(methodName) && ReturnsBool(returnType);

    /// <summary>Returns whether a method name begins with one of the curated security-check prefixes.</summary>
    /// <param name="methodName">The method name.</param>
    /// <returns><see langword="true"/> when the name has a security-check prefix.</returns>
    private static bool HasSecurityCheckPrefix(string methodName)
    {
        for (var i = 0; i < SecurityCheckPrefixes.Length; i++)
        {
            if (methodName.StartsWith(SecurityCheckPrefixes[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a return type is <c>bool</c>, <c>Task&lt;bool&gt;</c>, or <c>ValueTask&lt;bool&gt;</c>.</summary>
    /// <param name="returnType">The method return type.</param>
    /// <returns><see langword="true"/> for a boolean or boolean-task return type.</returns>
    private static bool ReturnsBool(TypeSyntax returnType)
    {
        var type = returnType is QualifiedNameSyntax qualified ? qualified.Right : returnType;
        return type switch
        {
            PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.BoolKeyword),
            GenericNameSyntax generic => IsTaskOfBool(generic),
            _ => false,
        };
    }

    /// <summary>Returns whether a generic name is <c>Task&lt;bool&gt;</c> or <c>ValueTask&lt;bool&gt;</c>.</summary>
    /// <param name="generic">The generic name syntax.</param>
    /// <returns><see langword="true"/> for a boolean task type.</returns>
    private static bool IsTaskOfBool(GenericNameSyntax generic)
        => generic.Identifier.ValueText is "Task" or "ValueTask"
            && generic.TypeArgumentList.Arguments.Count == 1
            && generic.TypeArgumentList.Arguments[0] is PredefinedTypeSyntax argument
            && argument.Keyword.IsKind(SyntaxKind.BoolKeyword);

    /// <summary>Returns whether the catch's only effect is to return success.</summary>
    /// <param name="catchClause">The catch clause under inspection.</param>
    /// <returns><see langword="true"/> when the catch returns success directly or falls through to one.</returns>
    private static bool CatchReturnsSuccess(CatchClauseSyntax catchClause)
    {
        var statements = catchClause.Block.Statements;
        StatementSyntax? single = null;
        var meaningfulCount = 0;
        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i].IsKind(SyntaxKind.EmptyStatement))
            {
                continue;
            }

            meaningfulCount++;
            if (meaningfulCount > 1)
            {
                return false;
            }

            single = statements[i];
        }

        return meaningfulCount == 1
            ? single is ReturnStatementSyntax returnStatement && IsSuccessExpression(returnStatement.Expression)
            : FallsThroughToSuccessReturn(catchClause);
    }

    /// <summary>Returns whether an empty catch falls through to a trailing success return in the enclosing block.</summary>
    /// <param name="catchClause">The empty catch clause.</param>
    /// <returns><see langword="true"/> when the statement following the try is a success return.</returns>
    private static bool FallsThroughToSuccessReturn(CatchClauseSyntax catchClause)
    {
        if (catchClause.Parent is not TryStatementSyntax tryStatement
            || tryStatement.Parent is not BlockSyntax block)
        {
            return false;
        }

        var statements = block.Statements;
        var tryIndex = statements.IndexOf(tryStatement);
        for (var i = tryIndex + 1; i < statements.Count; i++)
        {
            if (statements[i].IsKind(SyntaxKind.EmptyStatement))
            {
                continue;
            }

            return statements[i] is ReturnStatementSyntax returnStatement && IsSuccessExpression(returnStatement.Expression);
        }

        return false;
    }

    /// <summary>Returns whether an expression is the boolean literal <c>true</c> or a <c>FromResult(true)</c> call.</summary>
    /// <param name="expression">The returned expression.</param>
    /// <returns><see langword="true"/> when the expression is a success value.</returns>
    private static bool IsSuccessExpression(ExpressionSyntax? expression)
    {
        var unwrapped = Unwrap(expression);
        return unwrapped switch
        {
            LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.TrueLiteralExpression),
            InvocationExpressionSyntax invocation => IsFromResultTrue(invocation),
            _ => false,
        };
    }

    /// <summary>Returns whether an invocation is a <c>FromResult(true)</c> call (as used for a <c>Task&lt;bool&gt;</c> success).</summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <returns><see langword="true"/> for a <c>FromResult(true)</c> call.</returns>
    private static bool IsFromResultTrue(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: FromResultMethodName }
            || invocation.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        return Unwrap(invocation.ArgumentList.Arguments[0].Expression) is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.TrueLiteralExpression);
    }

    /// <summary>Strips redundant parentheses from an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? Unwrap(ExpressionSyntax? expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>Returns whether a catch swallows a broad or security-relevant exception.</summary>
    /// <param name="catchClause">The catch clause under inspection.</param>
    /// <param name="semanticModel">The semantic model.</param>
    /// <param name="knownExceptions">The resolved broad and security-relevant exception types.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the caught type is broad or security-relevant.</returns>
    private static bool CatchesBroadOrSecurityException(
        CatchClauseSyntax catchClause,
        SemanticModel semanticModel,
        KnownSecurityExceptions knownExceptions,
        CancellationToken cancellationToken)
    {
        // A bare 'catch' with no declaration catches every exception: broad.
        if (catchClause.Declaration is not { } declaration)
        {
            return true;
        }

        return semanticModel.GetTypeInfo(declaration.Type, cancellationToken).Type is INamedTypeSymbol caughtType
            && knownExceptions.IsBroadOrSecurityRelevant(caughtType);
    }

    /// <summary>Holds the broad and security-relevant exception types resolved once per compilation.</summary>
    private sealed class KnownSecurityExceptions
    {
        /// <summary>The <c>System.Exception</c> base type, or <see langword="null"/> when unavailable.</summary>
        private readonly INamedTypeSymbol? _exception;

        /// <summary>The <c>System.Security.Cryptography.CryptographicException</c> type, or <see langword="null"/> when unavailable.</summary>
        private readonly INamedTypeSymbol? _cryptographicException;

        /// <summary>The <c>System.Security.Authentication.AuthenticationException</c> type, or <see langword="null"/> when unavailable.</summary>
        private readonly INamedTypeSymbol? _authenticationException;

        /// <summary>Initializes a new instance of the <see cref="KnownSecurityExceptions"/> class.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        public KnownSecurityExceptions(Compilation compilation)
        {
            _exception = compilation.GetTypeByMetadataName("System.Exception");
            _cryptographicException = compilation.GetTypeByMetadataName("System.Security.Cryptography.CryptographicException");
            _authenticationException = compilation.GetTypeByMetadataName("System.Security.Authentication.AuthenticationException");
        }

        /// <summary>Returns whether a caught type is <c>System.Exception</c> or a security-relevant exception.</summary>
        /// <param name="caughtType">The declared caught exception type.</param>
        /// <returns><see langword="true"/> for a broad or security-relevant exception.</returns>
        public bool IsBroadOrSecurityRelevant(INamedTypeSymbol caughtType)
            => SymbolEqualityComparer.Default.Equals(caughtType, _exception)
                || SymbolEqualityComparer.Default.Equals(caughtType, _cryptographicException)
                || SymbolEqualityComparer.Default.Equals(caughtType, _authenticationException)
                || caughtType.Name.EndsWith(SecurityTokenExceptionSuffix, StringComparison.Ordinal);
    }
}
