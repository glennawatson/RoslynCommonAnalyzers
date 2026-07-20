// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a cleartext <c>http://</c> URL literal that points at a model-weights file (SES1606). The rule reports
/// any string literal whose text (compared case-insensitively) begins with <c>http://</c>, whose authority is a
/// non-loopback host, and whose path ends in a model-weights extension (<c>.onnx</c>, <c>.gguf</c>,
/// <c>.safetensors</c>, <c>.pt</c>, <c>.pth</c>, <c>.ckpt</c>). Downloading weights over cleartext lets a network
/// attacker substitute a tampered or backdoored model, so the weights extension combined with the http scheme is
/// treated as a high-confidence signal on its own -- the literal is reported wherever it appears (a constant, a
/// field, or an argument to any loader), independent of the consuming API. The scheme, host, and path extension are
/// parsed from the literal text with a single cheap scan and no <c>Uri</c> allocation, and loopback hosts
/// (<c>localhost</c>, <c>127.0.0.1</c>, <c>::1</c>, and any <c>*.localhost</c> host) are treated as clean because
/// cleartext there is expected in local development.
/// <para>
/// The transport rule that flags a cleartext http URL flowing into an <c>HttpClient</c> request already reports the
/// literal at that sink, so to avoid a duplicate diagnostic on the same span this rule stays silent when the literal
/// (directly, or wrapped in a <c>new Uri(...)</c>) is the URL argument of an <c>HttpClient</c> request method or is
/// assigned to <c>HttpClient.BaseAddress</c>; every other placement of a cleartext weights URL is reported here.
/// That exclusion is the rule's only use of the semantic model, and it runs only after the rare weights-URL literal
/// has already matched, so a project that never writes such a literal pays nothing.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1606CleartextModelWeightsUrlAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The cleartext scheme prefix the rule matches, compared case-insensitively.</summary>
    private const string HttpSchemePrefix = "http://";

    /// <summary>The metadata name of the HTTP client whose request sinks are deferred to the transport rule.</summary>
    private const string HttpClientMetadataName = "System.Net.Http.HttpClient";

    /// <summary>The name of the <c>HttpClient.BaseAddress</c> property whose assignment is deferred to the transport rule.</summary>
    private const string BaseAddressPropertyName = "BaseAddress";

    /// <summary>The model-weights path extensions that mark a URL as a weights download (lower-case, dotted).</summary>
    private static readonly string[] WeightsExtensions =
    [
        ".onnx",
        ".gguf",
        ".safetensors",
        ".pt",
        ".pth",
        ".ckpt",
    ];

    /// <summary>The HttpClient request methods whose URL literal the transport rule already owns (allocated once).</summary>
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
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.CleartextModelWeightsUrl);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // HttpClient is resolved only to suppress a duplicate at its request sink; the rule does NOT gate on it,
            // so a cleartext weights literal in a constant or a non-HttpClient loader is still reported when it is absent.
            var httpClientType = start.Compilation.GetTypeByMetadataName(HttpClientMetadataName);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeLiteral(nodeContext, httpClientType), SyntaxKind.StringLiteralExpression);
        });
    }

    /// <summary>Reports SES1606 for a cleartext-http model-weights string literal.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="httpClientType">The resolved <c>HttpClient</c> type, or <see langword="null"/> when absent.</param>
    private static void AnalyzeLiteral(SyntaxNodeAnalysisContext context, INamedTypeSymbol? httpClientType)
    {
        var literal = (LiteralExpressionSyntax)context.Node;

        // Syntactic-only clean path: parse scheme, host, and weights extension straight from the literal text.
        if (!IsCleartextWeightsUrl(literal.Token.ValueText, out var host))
        {
            return;
        }

        // Defer to the transport rule when the literal is the URL of an HttpClient request or BaseAddress assignment.
        if (httpClientType is not null
            && ReachesHttpClientSink(literal, httpClientType, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SecurityRules.CleartextModelWeightsUrl, literal.SyntaxTree, literal.Span, host));
    }

    /// <summary>Returns whether a literal's text is a cleartext-http URL to a non-loopback model-weights file.</summary>
    /// <param name="text">The decoded literal text.</param>
    /// <param name="host">When matched, the parsed non-loopback host of the URL.</param>
    /// <returns><see langword="true"/> for a reportable cleartext weights URL.</returns>
    private static bool IsCleartextWeightsUrl(string text, out string host)
    {
        host = string.Empty;
        if (text.Length <= HttpSchemePrefix.Length
            || !text.StartsWith(HttpSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // The path must end in a weights extension; a URL with no path (host only) can never match.
        var pathStart = FindPathStart(text);
        if (pathStart < 0 || !PathEndsInWeightsExtension(text, pathStart))
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

    /// <summary>Returns the index of the '/' that begins the URL path, or -1 when there is no path.</summary>
    /// <param name="text">The full URL text, already known to start with <c>http://</c>.</param>
    /// <returns>The path-start index, or -1 when the authority is not followed by a path.</returns>
    private static int FindPathStart(string text)
    {
        // Walk the authority; it ends at the first '/', '?' or '#'. Only a '/' opens a path with an extension.
        for (var i = HttpSchemePrefix.Length; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '/')
            {
                return i;
            }

            if (c is '?' or '#')
            {
                return -1;
            }
        }

        return -1;
    }

    /// <summary>Returns whether the path (from <paramref name="pathStart"/>, minus any query/fragment) ends in a weights extension.</summary>
    /// <param name="text">The full URL text.</param>
    /// <param name="pathStart">The index of the '/' that begins the path.</param>
    /// <returns><see langword="true"/> when the path ends in a model-weights extension.</returns>
    private static bool PathEndsInWeightsExtension(string text, int pathStart)
    {
        // The path ends at the first '?' or '#' after it begins (or at the end of the text).
        var pathEnd = text.Length;
        for (var i = pathStart; i < text.Length; i++)
        {
            if (text[i] is '?' or '#')
            {
                pathEnd = i;
                break;
            }
        }

        var pathLength = pathEnd - pathStart;
        for (var i = 0; i < WeightsExtensions.Length; i++)
        {
            var extension = WeightsExtensions[i];
            if (pathLength > extension.Length
                && string.Compare(text, pathEnd - extension.Length, extension, 0, extension.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
        }

        return false;
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

    /// <summary>Returns whether a matched literal is the URL of an <c>HttpClient</c> request or <c>BaseAddress</c> assignment.</summary>
    /// <param name="literal">The matched cleartext weights literal.</param>
    /// <param name="httpClientType">The resolved <c>HttpClient</c> type.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the literal is already owned by the transport rule.</returns>
    private static bool ReachesHttpClientSink(LiteralExpressionSyntax literal, INamedTypeSymbol httpClientType, SemanticModel model, CancellationToken cancellationToken)
    {
        if (literal.Parent is not ArgumentSyntax { Parent: ArgumentListSyntax { Parent: { } argumentListParent } } literalArgument)
        {
            return false;
        }

        return argumentListParent switch
        {
            // The literal is passed directly as the URL argument of an HttpClient request method.
            InvocationExpressionSyntax invocation =>
                IsHttpClientRequestUrl(invocation, literalArgument, httpClientType, model, cancellationToken),

            // The literal is the argument of a wrapping 'new Uri(...)' that is itself an HttpClient sink.
            ObjectCreationExpressionSyntax uriCreation =>
                UriCreationIsHttpClientSink(uriCreation, httpClientType, model, cancellationToken),

            _ => false,
        };
    }

    /// <summary>Returns whether a <c>new Uri(...)</c> holding the literal is an HttpClient request URL or BaseAddress value.</summary>
    /// <param name="uriCreation">The <c>new Uri(...)</c> wrapping the matched literal.</param>
    /// <param name="httpClientType">The resolved <c>HttpClient</c> type.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the created URI is already owned by the transport rule.</returns>
    private static bool UriCreationIsHttpClientSink(ObjectCreationExpressionSyntax uriCreation, INamedTypeSymbol httpClientType, SemanticModel model, CancellationToken cancellationToken)
        => uriCreation.Parent switch
        {
            ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } } uriArgument =>
                IsHttpClientRequestUrl(invocation, uriArgument, httpClientType, model, cancellationToken),

            AssignmentExpressionSyntax assignment when ReferenceEquals(assignment.Right, uriCreation) =>
                IsBaseAddressAssignment(assignment, httpClientType, model, cancellationToken),

            _ => false,
        };

    /// <summary>Returns whether an argument is the URL slot of an <c>HttpClient</c> request-method invocation.</summary>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="urlArgument">The argument that must be the invocation's URL slot.</param>
    /// <param name="httpClientType">The resolved <c>HttpClient</c> type.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for an HttpClient request method whose URL argument is <paramref name="urlArgument"/>.</returns>
    private static bool IsHttpClientRequestUrl(
        InvocationExpressionSyntax invocation,
        ArgumentSyntax urlArgument,
        INamedTypeSymbol httpClientType,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !RequestMethodNames.Contains(memberAccess.Name.Identifier.ValueText)
            || !ReferenceEquals(GetUrlArgument(invocation.ArgumentList), urlArgument))
        {
            return false;
        }

        return model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, httpClientType);
    }

    /// <summary>Returns whether an assignment sets <c>HttpClient.BaseAddress</c>.</summary>
    /// <param name="assignment">The candidate assignment.</param>
    /// <param name="httpClientType">The resolved <c>HttpClient</c> type.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for an <c>HttpClient.BaseAddress</c> assignment target.</returns>
    private static bool IsBaseAddressAssignment(AssignmentExpressionSyntax assignment, INamedTypeSymbol httpClientType, SemanticModel model, CancellationToken cancellationToken)
    {
        if (!IsBaseAddressTarget(assignment.Left))
        {
            return false;
        }

        return model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is IPropertySymbol { Name: BaseAddressPropertyName } property
            && SymbolEqualityComparer.Default.Equals(property.ContainingType, httpClientType);
    }

    /// <summary>Returns the request URL argument, honouring an explicit <c>requestUri:</c> name.</summary>
    /// <param name="argumentList">The invocation's argument list.</param>
    /// <returns>The URL argument, or <see langword="null"/> when it cannot be identified positionally.</returns>
    private static ArgumentSyntax? GetUrlArgument(ArgumentListSyntax argumentList)
    {
        var arguments = argumentList.Arguments;
        if (arguments.Count == 0)
        {
            return null;
        }

        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is { Name.Identifier.ValueText: "requestUri" })
            {
                return arguments[i];
            }
        }

        // The request URL is the first parameter of every guarded overload, so a leading positional argument is it.
        return arguments[0].NameColon is null ? arguments[0] : null;
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
}
