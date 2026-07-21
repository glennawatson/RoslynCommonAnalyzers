// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a Blazor <c>NavigationManager.NavigateTo</c> call whose <c>uri</c> target is not a verified
/// relative URL (SES1705). The rule reports the <c>uri</c> argument when the invoked method binds to
/// <c>Microsoft.AspNetCore.Components.NavigationManager</c> (or a subclass) and the target is neither a
/// verified relative literal nor produced by an allow-listed validator. A verified relative literal is a
/// compile-time-constant URL that is not absolute (no scheme) and not protocol-relative (does not begin
/// with two slashes), so the browser stays on the app's own origin. A non-constant target can carry an
/// attacker-supplied value, and an absolute or protocol-relative literal leaves the origin -- both are an
/// open redirect (CWE-601). The optional <c>securitysharp.SES1705.validators</c> option (falling back to
/// <c>securitysharp.validators</c>) lists method names whose result is trusted, so
/// <c>NavigateTo(Sanitize(url))</c> stays silent when <c>Sanitize</c> is allow-listed. The whole rule is
/// gated on <c>NavigationManager</c> resolving, so a non-Blazor project registers nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1705NavigationOpenRedirectAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the Blazor navigation service the rule gates on.</summary>
    private const string NavigationManagerMetadataName = "Microsoft.AspNetCore.Components.NavigationManager";

    /// <summary>The name of the navigation method whose target is guarded.</summary>
    private const string NavigateToMethodName = "NavigateTo";

    /// <summary>The name of the URL parameter on every <c>NavigateTo</c> overload.</summary>
    private const string UriParameterName = "uri";

    /// <summary>The zero-based position of the URL parameter on every <c>NavigateTo</c> overload.</summary>
    private const int UriPosition = 0;

    /// <summary>The rule-specific allow-listed-validator key.</summary>
    private const string ValidatorsRuleKey = "securitysharp.SES1705.validators";

    /// <summary>The project-wide allow-listed-validator key.</summary>
    private const string ValidatorsGeneralKey = "securitysharp.validators";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.NavigationOpenRedirect);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var navigationManager = start.Compilation.GetTypeByMetadataName(NavigationManagerMetadataName);
            if (navigationManager is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, navigationManager), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1705 for a <c>NavigateTo</c> call whose target is not a verified relative URL.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="navigationManager">The resolved <c>NavigationManager</c> type the rule gates on.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol navigationManager)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a call to 'NavigateTo' carrying at least the URL argument.
        if (invocation.ArgumentList.Arguments.Count == 0
            || !string.Equals(BlazorInvocation.GetInvokedName(invocation.Expression), NavigateToMethodName, StringComparison.Ordinal))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: NavigateToMethodName } method
            || !IsOrDerivesFrom(method.ContainingType, navigationManager)
            || BlazorInvocation.GetArgument(invocation.ArgumentList, UriParameterName, UriPosition) is not { } uriArgument)
        {
            return;
        }

        // A verified relative literal stays on the app's own origin and cannot be steered.
        if (context.SemanticModel.GetConstantValue(uriArgument, context.CancellationToken).Value is string constantUri
            && IsVerifiedRelative(constantUri))
        {
            return;
        }

        if (IsProducedByAllowListedValidator(context, uriArgument))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.NavigationOpenRedirect,
            uriArgument.SyntaxTree,
            uriArgument.Span,
            method.Name));
    }

    /// <summary>Returns whether a constant URL is relative to the app's own origin (not absolute, not protocol-relative).</summary>
    /// <param name="url">The constant URL value.</param>
    /// <returns><see langword="true"/> when the URL stays on the current origin and cannot redirect off-site.</returns>
    private static bool IsVerifiedRelative(string url)
    {
        // An empty target navigates within the app and cannot leave the origin.
        if (url.Length == 0)
        {
            return true;
        }

        // A protocol-relative reference ('//host', and the '/\' / '\\' browser-equivalent tricks) leaves the origin.
        if (url.Length >= 2 && IsSlash(url[0]) && IsSlash(url[1]))
        {
            return false;
        }

        // An explicit URI scheme ('https:', 'javascript:', ...) makes the target absolute, so it leaves the
        // origin; anything else (a rooted '/path', a base-relative 'path', '~/path', a '#fragment') resolves
        // against the app's base URI. A leading slash is deliberately not treated as absolute here: the
        // platform 'Uri' parser reads '/path' as an absolute file URI on Unix, which this rule must not.
        return !HasUriScheme(url);
    }

    /// <summary>Returns whether a character is a forward or back slash, both of which a browser treats as a path separator.</summary>
    /// <param name="value">The character to test.</param>
    /// <returns><see langword="true"/> for <c>'/'</c> or <c>'\\'</c>.</returns>
    private static bool IsSlash(char value) => value is '/' or '\\';

    /// <summary>Returns whether a URL begins with an explicit URI scheme (<c>scheme:</c>) per RFC 3986.</summary>
    /// <param name="url">The URL value.</param>
    /// <returns><see langword="true"/> when a scheme delimiter is reached before any path, query, or fragment.</returns>
    private static bool HasUriScheme(string url)
    {
        // scheme = ALPHA *( ALPHA / DIGIT / '+' / '-' / '.' ) followed by ':'.
        if (!IsSchemeFirstChar(url[0]))
        {
            return false;
        }

        for (var i = 1; i < url.Length; i++)
        {
            var current = url[i];
            if (current == ':')
            {
                return true;
            }

            if (!IsSchemeChar(current))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Returns whether a character may start a URI scheme (an ASCII letter).</summary>
    /// <param name="value">The character to test.</param>
    /// <returns><see langword="true"/> for <c>a</c>-<c>z</c> or <c>A</c>-<c>Z</c>.</returns>
    private static bool IsSchemeFirstChar(char value)
        => value is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    /// <summary>Returns whether a character may appear inside a URI scheme (letter, digit, <c>+</c>, <c>-</c>, or <c>.</c>).</summary>
    /// <param name="value">The character to test.</param>
    /// <returns><see langword="true"/> for a valid scheme character.</returns>
    private static bool IsSchemeChar(char value)
        => IsSchemeFirstChar(value) || value is (>= '0' and <= '9') or '+' or '-' or '.';

    /// <summary>Returns whether the target expression is a call to a validator named in the allow-list option.</summary>
    /// <param name="context">The syntax node analysis context, used to read the allow-list option.</param>
    /// <param name="uriArgument">The URL argument expression.</param>
    /// <returns><see langword="true"/> when the target is produced by an allow-listed validator and is therefore trusted.</returns>
    private static bool IsProducedByAllowListedValidator(SyntaxNodeAnalysisContext context, ExpressionSyntax uriArgument)
    {
        var expression = uriArgument;
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        if (expression is not InvocationExpressionSyntax validatorInvocation
            || BlazorInvocation.GetInvokedName(validatorInvocation.Expression) is not { } validatorName)
        {
            return false;
        }

        var validators = AnalyzerOptionReader.ReadCommaSeparatedList(
            context.Options.AnalyzerConfigOptionsProvider.GetOptions(uriArgument.SyntaxTree),
            ValidatorsRuleKey,
            ValidatorsGeneralKey);

        for (var i = 0; i < validators.Length; i++)
        {
            if (string.Equals(validators[i], validatorName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is, or derives from, the gated <c>NavigationManager</c> type.</summary>
    /// <param name="type">The bound method's containing type.</param>
    /// <param name="navigationManager">The resolved <c>NavigationManager</c> type.</param>
    /// <returns><see langword="true"/> when the type is <c>NavigationManager</c> or a subclass of it.</returns>
    private static bool IsOrDerivesFrom(INamedTypeSymbol type, INamedTypeSymbol navigationManager)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, navigationManager))
            {
                return true;
            }
        }

        return false;
    }
}
