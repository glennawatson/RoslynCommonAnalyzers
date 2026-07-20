// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// <summary>The metadata name of the HTTP client whose request sinks are guarded.</summary>
    private const string HttpClientMetadataName = "System.Net.Http.HttpClient";

    /// <summary>The name of the <c>HttpClient.BaseAddress</c> property whose assignment is inspected.</summary>
    private const string BaseAddressPropertyName = "BaseAddress";

    /// <summary>The cleartext scheme prefix the rule matches, compared case-insensitively.</summary>
    private const string HttpSchemePrefix = "http://";

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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.CleartextHttpUrl);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var httpClientType = start.Compilation.GetTypeByMetadataName(HttpClientMetadataName);
            if (httpClientType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, httpClientType), SyntaxKind.InvocationExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, httpClientType), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1106 for an HttpClient request method given a cleartext URL literal.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="httpClientType">The resolved <c>HttpClient</c> type.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol httpClientType)
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
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, httpClientType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SecurityRules.CleartextHttpUrl, literal.SyntaxTree, literal.Span, host));
    }

    /// <summary>Reports SES1106 for a <c>HttpClient.BaseAddress = new Uri("http://…")</c> assignment.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="httpClientType">The resolved <c>HttpClient</c> type.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol httpClientType)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: 'someMember.BaseAddress = new Uri("http://…")' or 'BaseAddress = new Uri("http://…")'.
        if (!IsBaseAddressTarget(assignment.Left)
            || GetUriCreationLiteral(assignment.Right, out var host) is not { } literal)
        {
            return;
        }

        // Semantic confirmation: the assigned member is HttpClient.BaseAddress (the property is Uri-typed).
        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol { Name: BaseAddressPropertyName } property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, httpClientType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SecurityRules.CleartextHttpUrl, literal.SyntaxTree, literal.Span, host));
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
    private static LiteralExpressionSyntax? GetCleartextHttpLiteral(ExpressionSyntax urlArgument, out string host)
    {
        if (urlArgument is LiteralExpressionSyntax stringLiteral && IsCleartextHttpLiteral(stringLiteral, out host))
        {
            return stringLiteral;
        }

        if (urlArgument is ObjectCreationExpressionSyntax objectCreation)
        {
            return GetUriCreationLiteral(objectCreation, out host);
        }

        host = string.Empty;
        return null;
    }

    /// <summary>Returns the cleartext-http literal that is the sole/URL argument of a <c>new Uri(...)</c>.</summary>
    /// <param name="expression">The candidate <c>new Uri(...)</c> expression.</param>
    /// <param name="host">When matched, the parsed non-loopback host of the URL.</param>
    /// <returns>The cleartext-http string literal, or <see langword="null"/> when the shape does not match.</returns>
    private static LiteralExpressionSyntax? GetUriCreationLiteral(ExpressionSyntax expression, out string host)
    {
        host = string.Empty;
        if (expression is not ObjectCreationExpressionSyntax { ArgumentList: { } argumentList }
            || argumentList.Arguments.Count == 0
            || GetUriStringArgument(argumentList) is not LiteralExpressionSyntax stringLiteral
            || !IsCleartextHttpLiteral(stringLiteral, out host))
        {
            return null;
        }

        return stringLiteral;
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
    private static bool IsBaseAddressTarget(ExpressionSyntax left)
        => left switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: BaseAddressPropertyName } => true,
            IdentifierNameSyntax { Identifier.ValueText: BaseAddressPropertyName } => true,
            _ => false,
        };

    /// <summary>Returns whether a string literal is a cleartext <c>http://</c> URL with a non-loopback host.</summary>
    /// <param name="literal">The candidate string literal.</param>
    /// <param name="host">When matched, the parsed non-loopback host.</param>
    /// <returns><see langword="true"/> for a reportable cleartext-http literal.</returns>
    private static bool IsCleartextHttpLiteral(LiteralExpressionSyntax literal, out string host)
    {
        host = string.Empty;
        if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return false;
        }

        var text = literal.Token.ValueText;
        if (text.Length <= HttpSchemePrefix.Length
            || !text.StartsWith(HttpSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parsedHost = ExtractHost(text);
        if (parsedHost.Length == 0 || IsLoopbackHost(parsedHost))
        {
            return false;
        }

        host = parsedHost;
        return true;
    }

    /// <summary>Extracts the host of a URL text cheaply, without allocating a <c>Uri</c>.</summary>
    /// <param name="text">The full URL text, already known to start with <c>http://</c>.</param>
    /// <returns>The host segment; a bracketed IPv6 authority is returned without its brackets.</returns>
    private static string ExtractHost(string text)
    {
        var start = HttpSchemePrefix.Length;

        // A bracketed IPv6 authority (e.g. '[::1]') carries colons, so read the inner address to the closing bracket.
        if (text[start] == '[')
        {
            var inner = start + 1;
            var close = text.IndexOf(']', inner);
            return close < 0 ? text.Substring(inner) : text.Substring(inner, close - inner);
        }

        var end = start;
        while (end < text.Length)
        {
            var c = text[end];
            if (c is '/' or ':' or '?' or '#')
            {
                break;
            }

            end++;
        }

        return text.Substring(start, end - start);
    }

    /// <summary>Returns whether a parsed host is a loopback address that does not warrant a cleartext warning.</summary>
    /// <param name="host">The parsed host.</param>
    /// <returns><see langword="true"/> for a loopback or <c>*.localhost</c> host.</returns>
    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
            || string.Equals(host, "::1", StringComparison.Ordinal))
        {
            return true;
        }

        return host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }
}
