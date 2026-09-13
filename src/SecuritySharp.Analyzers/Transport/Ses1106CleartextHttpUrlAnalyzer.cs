// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a cleartext <c>http://</c> URL literal that flows into a <c>System.Net.Http.HttpClient</c> request
/// (SES1106). The rule reports the string literal in three local, high-precision shapes: the URL string passed
/// directly to an HttpClient request method (<c>GetAsync</c>, <c>GetStringAsync</c>, <c>GetByteArrayAsync</c>,
/// <c>GetStreamAsync</c>, <c>PostAsync</c>, <c>PutAsync</c>, <c>PatchAsync</c>, <c>DeleteAsync</c>,
/// <c>SendAsync</c>); the string inside a <c>new System.Uri(...)</c> that is itself the request argument; and the
/// string inside a <c>new System.Uri(...)</c> assigned to <c>HttpClient.BaseAddress</c>. The invoked method or
/// assigned member is bound to confirm the container is <c>HttpClient</c> — a name match alone is never trusted,
/// and because the request-URL slot and <c>BaseAddress</c> are both <c>Uri</c>-typed, an object creation there is
/// necessarily a <c>Uri</c>. Loopback hosts (<c>localhost</c>, <c>127.0.0.1</c>, <c>::1</c>, <c>[::1]</c>, and any
/// <c>*.localhost</c> host) are treated as clean because cleartext there is expected in local development. Only a
/// literal is examined; a URL held in a variable or returned by a call is deliberately not tracked, keeping the
/// rule fast and free of false positives. The rule is gated on <c>HttpClient</c> resolving in the compilation, so a
/// project that cannot make these calls pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1106CleartextHttpUrlAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the <c>HttpClient.BaseAddress</c> property whose assignment is inspected.</summary>
    private const string BaseAddressPropertyName = "BaseAddress";

    /// <summary>The HttpClient request methods whose URL argument is inspected (allocated once).</summary>
    private static readonly HashSet<string> RequestMethodNames = new(StringComparer.Ordinal)
    {
        "GetAsync",
        "GetStringAsync",
        "GetByteArrayAsync",
        "GetStreamAsync",
        "PostAsync",
        "PutAsync",
        "PatchAsync",
        "DeleteAsync",
        "SendAsync",
    };

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.CleartextHttpUrl);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static startContext =>
        {
            var types = new HttpClientTypes(startContext.Compilation);
            startContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, types), SyntaxKind.InvocationExpression);
            startContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, types), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1106 for an HttpClient request method given a cleartext URL literal.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation-scoped HTTP client type cache.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, HttpClientTypes types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.<RequestMethod>(...)' call carrying at least one argument.
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !RequestMethodNames.Contains(memberAccess.Name.Identifier.ValueText)
            || invocation.ArgumentList.Arguments.Count == 0
            || GetUrlArgument(invocation.ArgumentList) is not { } urlArgument
            || GetCleartextHttpLiteral(urlArgument, out var host) is not { } literal)
        {
            return;
        }

        // Semantic confirmation only after the cheap syntactic path has already matched a cleartext literal.
        if (types.Get() is not { } httpClientType
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, httpClientType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SecurityRules.CleartextHttpUrl, literal.SyntaxTree, literal.Span, host.ToString()));
    }

    /// <summary>Reports SES1106 for a <c>HttpClient.BaseAddress = new Uri("http://…")</c> assignment.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation-scoped HTTP client type cache.</param>
    private static void AnalyzeAssignment(in SyntaxNodeAnalysisContext context, HttpClientTypes types)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: 'someMember.BaseAddress = new Uri("http://…")' or 'BaseAddress = new Uri("http://…")'.
        if (!IsBaseAddressTarget(assignment.Left)
            || GetUriCreationLiteral(assignment.Right, out var host) is not { } literal)
        {
            return;
        }

        // Semantic confirmation: the assigned member is HttpClient.BaseAddress (the property is Uri-typed).
        if (types.Get() is not { } httpClientType
            || context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol { Name: BaseAddressPropertyName } property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, httpClientType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SecurityRules.CleartextHttpUrl, literal.SyntaxTree, literal.Span, host.ToString()));
    }

    /// <summary>Returns the request URL argument, honouring an explicit <c>requestUri:</c> name.</summary>
    /// <param name="argumentList">The invocation's argument list.</param>
    /// <returns>The URL argument expression, or <see langword="null"/> when it cannot be identified positionally.</returns>
    private static ExpressionSyntax? GetUrlArgument(ArgumentListSyntax argumentList)
    {
        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { Name.Identifier.ValueText: "requestUri" })
            {
                return arguments[i].Expression;
            }
        }

        // The request URL is the first parameter of every guarded overload, so a leading positional argument is it.
        return arguments[0].NameColon is null ? arguments[0].Expression : null;
    }

    /// <summary>Returns the cleartext-http literal reached through a URL argument (direct string or <c>new Uri(...)</c>).</summary>
    /// <param name="urlArgument">The request URL argument expression.</param>
    /// <param name="host">When matched, the parsed non-loopback host of the URL.</param>
    /// <returns>The cleartext-http string literal, or <see langword="null"/> when the argument is not one.</returns>
    private static LiteralExpressionSyntax? GetCleartextHttpLiteral(ExpressionSyntax urlArgument, out ReadOnlySpan<char> host)
    {
        if (urlArgument is LiteralExpressionSyntax stringLiteral && IsCleartextHttpLiteral(stringLiteral, out host))
        {
            return stringLiteral;
        }

        if (urlArgument is ObjectCreationExpressionSyntax objectCreation)
        {
            return GetUriCreationLiteral(objectCreation, out host);
        }

        host = default;
        return null;
    }

    /// <summary>Returns the cleartext-http literal that is the sole/URL argument of a <c>new Uri(...)</c>.</summary>
    /// <param name="expression">The candidate <c>new Uri(...)</c> expression.</param>
    /// <param name="host">When matched, the parsed non-loopback host of the URL.</param>
    /// <returns>The cleartext-http string literal, or <see langword="null"/> when the shape does not match.</returns>
    private static LiteralExpressionSyntax? GetUriCreationLiteral(ExpressionSyntax expression, out ReadOnlySpan<char> host)
    {
        host = default;
        return expression is not ObjectCreationExpressionSyntax { ArgumentList: { } argumentList }
            || argumentList.Arguments.Count == 0
            || GetUriStringArgument(argumentList) is not LiteralExpressionSyntax stringLiteral
            || !IsCleartextHttpLiteral(stringLiteral, out host)
            ? null
            : stringLiteral;
    }

    /// <summary>Returns the URI-string argument of a <c>new Uri(...)</c>, honouring an explicit <c>uriString:</c> name.</summary>
    /// <param name="argumentList">The object-creation argument list.</param>
    /// <returns>The URI-string argument expression, or <see langword="null"/> when it cannot be identified positionally.</returns>
    private static ExpressionSyntax? GetUriStringArgument(ArgumentListSyntax argumentList)
    {
        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { Name.Identifier.ValueText: "uriString" })
            {
                return arguments[i].Expression;
            }
        }

        return arguments[0].NameColon is null ? arguments[0].Expression : null;
    }

    /// <summary>Returns whether an assignment target names the <c>BaseAddress</c> member.</summary>
    /// <param name="left">The assignment's left-hand side.</param>
    /// <returns><see langword="true"/> for a <c>.BaseAddress</c> or bare <c>BaseAddress</c> target.</returns>
    private static bool IsBaseAddressTarget(ExpressionSyntax left) =>
        left switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: BaseAddressPropertyName } or IdentifierNameSyntax { Identifier.ValueText: BaseAddressPropertyName } => true,
            _ => false,
        };

    /// <summary>Returns whether a string literal is a cleartext <c>http://</c> URL with a non-loopback host.</summary>
    /// <param name="literal">The candidate string literal.</param>
    /// <param name="host">When matched, the parsed non-loopback host.</param>
    /// <returns><see langword="true"/> for a reportable cleartext-http literal.</returns>
    private static bool IsCleartextHttpLiteral(LiteralExpressionSyntax literal, out ReadOnlySpan<char> host)
    {
        host = default;
        if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return false;
        }

        var text = literal.Token.ValueText;
        if (text.Length <= CleartextUrl.HttpSchemePrefix.Length
            || !text.StartsWith(CleartextUrl.HttpSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parsedHost = CleartextUrl.ExtractHost(text);
        if (parsedHost.IsEmpty || CleartextUrl.IsLoopbackHost(parsedHost))
        {
            return false;
        }

        host = parsedHost;
        return true;
    }

    /// <summary>Resolves the HTTP client type on first demand within a compilation.</summary>
    /// <param name="compilation">The compilation whose HTTP client type is resolved.</param>
    private sealed class HttpClientTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the HTTP client whose request sinks are guarded.</summary>
        private const string HttpClientMetadataName = "System.Net.Http.HttpClient";

        /// <summary>The cached type, with a null element when the type is absent.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Resolves the HTTP client type on first demand and caches its absence too.</summary>
        /// <returns>The HTTP client type, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName(HttpClientMetadataName)])[0];
    }
}
