// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
/// ignored. The <c>ControllerBase</c> type is resolved once per compilation, only after a call passes the
/// syntactic filter, and the rule reports nothing when the type is absent.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1510NonConstantControllerRedirectAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The number of arguments a guarded redirect helper takes: the single URL string.</summary>
    private const int RedirectArgumentCount = 1;

    /// <summary>The metadata name of the controller base type whose redirect helpers are guarded.</summary>
    private const string ControllerBaseMetadataName = "Microsoft.AspNetCore.Mvc.ControllerBase";

    /// <summary>The names of the <c>ControllerBase</c> redirect helpers that take a raw URL string.</summary>
    private static readonly string[] RedirectMethodNames =
    [
        "Redirect",
        "RedirectPermanent",
        "RedirectPreserveMethod",
        "RedirectPermanentPreserveMethod"
    ];

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.NonConstantControllerRedirect);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyCompilationValue<INamedTypeSymbol?>(compilation, ResolveControllerBaseType, runOnce: true),
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports SES1510 for a controller redirect helper whose URL argument is non-constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="controllerBase">The lazily resolved <c>ControllerBase</c> type the rule gates on.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyCompilationValue<INamedTypeSymbol?> controllerBase)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a call to one of the redirect helper names carrying exactly the URL argument.
        if (invocation.ArgumentList.Arguments.Count != RedirectArgumentCount
            || !IsRedirectHelperName(MemberReferenceName.Of(invocation.Expression)))
        {
            return;
        }

        var urlArgument = invocation.ArgumentList.Arguments[0].Expression;

        // Constant targets are out of scope, so reject them before resolving controller metadata.
        if (urlArgument.IsKind(SyntaxKind.StringLiteralExpression)
            || urlArgument.IsKind(SyntaxKind.NullLiteralExpression)
            || context.SemanticModel.GetConstantValue(urlArgument, context.CancellationToken).HasValue)
        {
            return;
        }

        if (controllerBase.Get() is not { } controllerBaseType
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsRedirectHelperName(method.Name)
            || !TypeRelations.IsOrDerivesFrom(method.ContainingType, controllerBaseType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.NonConstantControllerRedirect,
            urlArgument.SyntaxTree,
            urlArgument.Span,
            method.Name));
    }

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

    /// <summary>Resolves the controller base type for a compilation.</summary>
    /// <param name="compilation">The compilation whose controller type is resolved.</param>
    /// <returns>The controller type, or <see langword="null"/> when it is absent.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static INamedTypeSymbol? ResolveControllerBaseType(Compilation compilation) =>
        compilation.GetTypeByMetadataName(ControllerBaseMetadataName);
}
