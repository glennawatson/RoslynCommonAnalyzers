// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags an MVC controller redirect to a non-constant URL (SES1510). The rule reports the URL argument
/// of <c>Redirect</c>, <c>RedirectPermanent</c>, <c>RedirectPreserveMethod</c>, and
/// <c>RedirectPermanentPreserveMethod</c> when the invoked method's containing type is (or derives from)
/// <c>Microsoft.AspNetCore.Mvc.ControllerBase</c> and the URL argument is not a compile-time constant. A
/// non-constant target can carry an attacker-supplied value, sending the browser to an external phishing
/// site (CWE-601, open redirect); a hard-coded literal URL cannot be steered and is not reported. The
/// <c>LocalRedirect*</c> family (already local-only) and the <c>RedirectToAction</c>/<c>RedirectToRoute</c>/
/// <c>RedirectToPage</c> helpers (which take action/route/page names, not a URL) are never flagged. The
/// method is bound and its container matched by symbol, so a same-named method on an unrelated type is
/// ignored. The whole rule is gated on <c>ControllerBase</c> resolving, so a non-ASP.NET project registers
/// nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1510NonConstantControllerRedirectAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the controller base type whose redirect helpers are guarded.</summary>
    private const string ControllerBaseMetadataName = "Microsoft.AspNetCore.Mvc.ControllerBase";

    /// <summary>The number of arguments a guarded redirect helper takes: the single URL string.</summary>
    private const int RedirectArgumentCount = 1;

    /// <summary>The names of the <c>ControllerBase</c> redirect helpers that take a raw URL string.</summary>
    private static readonly string[] RedirectMethodNames =
    [
        "Redirect",
        "RedirectPermanent",
        "RedirectPreserveMethod",
        "RedirectPermanentPreserveMethod"
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.NonConstantControllerRedirect);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var controllerBase = start.Compilation.GetTypeByMetadataName(ControllerBaseMetadataName);
            if (controllerBase is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, controllerBase), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1510 for a controller redirect helper whose URL argument is non-constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="controllerBase">The resolved <c>ControllerBase</c> type the rule gates on.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol controllerBase)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a call to one of the redirect helper names carrying exactly the URL argument.
        if (invocation.ArgumentList.Arguments.Count != RedirectArgumentCount
            || !IsRedirectHelperName(GetInvokedName(invocation.Expression)))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsRedirectHelperName(method.Name)
            || !IsOrDerivesFrom(method.ContainingType, controllerBase))
        {
            return;
        }

        var urlArgument = invocation.ArgumentList.Arguments[0].Expression;

        // A hard-coded literal URL cannot be steered by an attacker, so it is out of scope.
        if (context.SemanticModel.GetConstantValue(urlArgument, context.CancellationToken).HasValue)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.NonConstantControllerRedirect,
            urlArgument.SyntaxTree,
            urlArgument.Span,
            method.Name));
    }

    /// <summary>Returns the invoked member's simple name for an <c>Identifier(...)</c> or <c>x.Identifier(...)</c> call.</summary>
    /// <param name="expression">The invocation's callee expression.</param>
    /// <returns>The simple name, or <see langword="null"/> when the callee is not a plain member reference.</returns>
    private static string? GetInvokedName(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns whether a name is one of the guarded URL-taking redirect helpers.</summary>
    /// <param name="name">The candidate method name.</param>
    /// <returns><see langword="true"/> when the name matches a guarded redirect helper exactly.</returns>
    private static bool IsRedirectHelperName(string? name)
    {
        if (name is null)
        {
            return false;
        }

        for (var i = 0; i < RedirectMethodNames.Length; i++)
        {
            if (string.Equals(RedirectMethodNames[i], name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is, or derives from, the gated <c>ControllerBase</c> type.</summary>
    /// <param name="type">The bound method's containing type.</param>
    /// <param name="controllerBase">The resolved <c>ControllerBase</c> type.</param>
    /// <returns><see langword="true"/> when the type is <c>ControllerBase</c> or a subclass of it.</returns>
    private static bool IsOrDerivesFrom(INamedTypeSymbol type, INamedTypeSymbol controllerBase)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, controllerBase))
            {
                return true;
            }
        }

        return false;
    }
}
