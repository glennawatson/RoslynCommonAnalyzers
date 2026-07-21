// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a synchronous read or write of the HTTP request or response body (PSH1506): a
/// <c>Read</c>/<c>ReadByte</c>/<c>Write</c>/<c>WriteByte</c>/<c>Flush</c>/<c>CopyTo</c> call whose
/// receiver is <c>HttpRequest.Body</c> or <c>HttpResponse.Body</c>, or a
/// <c>ReadToEnd</c>/<c>ReadLine</c>/<c>ReadBlock</c> call whose receiver is a <c>StreamReader</c>
/// (or <c>StreamWriter</c>) constructed over one of those streams. The receiver may be the body
/// property written inline (<c>ctx.Request.Body.Read(…)</c>,
/// <c>new StreamReader(ctx.Request.Body).ReadToEnd()</c>) or a local variable initialised from it.
/// Blocking a pooled thread on the connection's socket is how a server starves its thread pool
/// under load, and the synchronous form buffers the payload unbounded.
/// </summary>
/// <remarks>
/// The whole rule is gated at compilation start on <c>Microsoft.AspNetCore.Http.HttpRequest</c>
/// resolving; a project that does not reference ASP.NET Core registers no syntax action. The clean
/// path fails fast on a syntactic member-name prefilter before the semantic model is consulted, and
/// the async replacement is resolved on the receiver's own type — a member named after the async
/// sibling must exist there — so a framework that cannot offer the overload is never reported. The
/// analyzer reports the defect wherever it occurs; only the code fix requires an <c>async</c> context.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1506SynchronousBodyIoAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the request type whose <c>Body</c> the rule watches, and the compilation gate.</summary>
    private const string HttpRequestMetadataName = "Microsoft.AspNetCore.Http.HttpRequest";

    /// <summary>The metadata name of the response type whose <c>Body</c> the rule watches.</summary>
    private const string HttpResponseMetadataName = "Microsoft.AspNetCore.Http.HttpResponse";

    /// <summary>The name of the body property on the request and response types.</summary>
    private const string BodyPropertyName = "Body";

    /// <summary>The reader wrapper type whose synchronous reads over the body are reported.</summary>
    private const string StreamReaderTypeName = "StreamReader";

    /// <summary>The writer wrapper type whose synchronous writes over the body are reported.</summary>
    private const string StreamWriterTypeName = "StreamWriter";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AspNetCoreRules.SynchronousBodyIo);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var httpRequest = start.Compilation.GetTypeByMetadataName(HttpRequestMetadataName);
            if (httpRequest is null)
            {
                return;
            }

            var gate = new BodyGate(httpRequest, start.Compilation.GetTypeByMetadataName(HttpResponseMetadataName));
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, gate), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Maps a synchronous stream or reader I/O method to the async method that replaces it.</summary>
    /// <param name="syncName">The invoked member's simple name.</param>
    /// <returns>The async counterpart's name, or <see langword="null"/> when the name is not a synchronous I/O method.</returns>
    internal static string? AsyncCounterpartName(string syncName) => syncName switch
    {
        "Read" or "ReadByte" => "ReadAsync",
        "Write" or "WriteByte" => "WriteAsync",
        "Flush" => "FlushAsync",
        "CopyTo" => "CopyToAsync",
        _ => ReaderCounterpartName(syncName),
    };

    /// <summary>Maps a synchronous text-reader I/O method to the async method that replaces it.</summary>
    /// <param name="syncName">The invoked member's simple name.</param>
    /// <returns>The async counterpart's name, or <see langword="null"/> when the name is not a reader I/O method.</returns>
    private static string? ReaderCounterpartName(string syncName) => syncName switch
    {
        "ReadToEnd" => "ReadToEndAsync",
        "ReadLine" => "ReadLineAsync",
        "ReadBlock" => "ReadBlockAsync",
        _ => null,
    };

    /// <summary>Reports PSH1506 when a synchronous I/O call binds to the HTTP body and an async overload exists on the receiver.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="gate">The resolved ASP.NET Core body-owning types.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, BodyGate gate)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || AsyncCounterpartName(access.Name.Identifier.ValueText) is not { } asyncName)
        {
            return;
        }

        if (!ReceiverReadsHttpBody(context.SemanticModel, access.Expression, gate, context.CancellationToken))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !AsyncOverloadExists(method.ContainingType, asyncName))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AspNetCoreRules.SynchronousBodyIo,
            invocation.SyntaxTree,
            invocation.Span,
            asyncName));
    }

    /// <summary>Returns whether a call's receiver reads or writes the HTTP body.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="receiver">The receiver expression of the synchronous call.</param>
    /// <param name="gate">The resolved ASP.NET Core body-owning types.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the receiver is the body stream, a wrapper over it, or a local initialised from one.</returns>
    private static bool ReceiverReadsHttpBody(SemanticModel model, ExpressionSyntax receiver, BodyGate gate, CancellationToken cancellationToken)
    {
        var expr = Unwrap(receiver);
        if (expr is ObjectCreationExpressionSyntax creation)
        {
            return CreationWrapsBody(model, creation, gate, cancellationToken);
        }

        return model.GetSymbolInfo(expr, cancellationToken).Symbol switch
        {
            IPropertySymbol property => IsBodyProperty(property, gate),
            ILocalSymbol local => LocalInitializerReadsBody(model, local, gate, cancellationToken),
            _ => false,
        };
    }

    /// <summary>Returns whether a local variable is initialised from the HTTP body or a wrapper over it.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="local">The local symbol the call is made on.</param>
    /// <param name="gate">The resolved ASP.NET Core body-owning types.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the local's initializer binds to the body stream or a reader/writer over it.</returns>
    private static bool LocalInitializerReadsBody(SemanticModel model, ILocalSymbol local, BodyGate gate, CancellationToken cancellationToken)
    {
        if (TryGetLocalInitializer(local, cancellationToken) is not { } initializer)
        {
            return false;
        }

        var init = Unwrap(initializer);
        return init is ObjectCreationExpressionSyntax creation
            ? CreationWrapsBody(model, creation, gate, cancellationToken)
            : model.GetSymbolInfo(init, cancellationToken).Symbol is IPropertySymbol property && IsBodyProperty(property, gate);
    }

    /// <summary>Returns whether an object creation is a stream reader or writer constructed over the HTTP body.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="creation">The object creation to inspect.</param>
    /// <param name="gate">The resolved ASP.NET Core body-owning types.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a wrapper type receives the body stream as a constructor argument.</returns>
    private static bool CreationWrapsBody(SemanticModel model, ObjectCreationExpressionSyntax creation, BodyGate gate, CancellationToken cancellationToken)
    {
        if (GetSimpleTypeName(creation.Type) is not (StreamReaderTypeName or StreamWriterTypeName)
            || creation.ArgumentList is not { } argumentList)
        {
            return false;
        }

        var arguments = argumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (model.GetSymbolInfo(Unwrap(arguments[i].Expression), cancellationToken).Symbol is IPropertySymbol property
                && IsBodyProperty(property, gate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a property is the <c>Body</c> of the request or response type.</summary>
    /// <param name="property">The candidate property.</param>
    /// <param name="gate">The resolved ASP.NET Core body-owning types.</param>
    /// <returns><see langword="true"/> when the property is <c>Body</c> declared on a request or response type.</returns>
    private static bool IsBodyProperty(IPropertySymbol property, BodyGate gate)
    {
        if (property.Name != BodyPropertyName)
        {
            return false;
        }

        for (var current = property.ContainingType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, gate.HttpRequest)
                || (gate.HttpResponse is not null && SymbolEqualityComparer.Default.Equals(current, gate.HttpResponse)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a member named after the async sibling exists on the receiver's type hierarchy.</summary>
    /// <param name="containingType">The type declaring the synchronous method that was called.</param>
    /// <param name="asyncName">The async counterpart's name.</param>
    /// <returns><see langword="true"/> when the receiver exposes a method by that name.</returns>
    private static bool AsyncOverloadExists(INamedTypeSymbol containingType, string asyncName)
    {
        for (var current = containingType; current is not null; current = current.BaseType)
        {
            var members = current.GetMembers(asyncName);
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i] is IMethodSymbol)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns the initializer of a local's declaring variable, when it has one.</summary>
    /// <param name="local">The local symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The initializer expression, or <see langword="null"/> when the local has no simple initializer.</returns>
    private static ExpressionSyntax? TryGetLocalInitializer(ILocalSymbol local, CancellationToken cancellationToken)
    {
        var references = local.DeclaringSyntaxReferences;
        for (var i = 0; i < references.Length; i++)
        {
            if (references[i].GetSyntax(cancellationToken) is VariableDeclaratorSyntax { Initializer.Value: { } value })
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Peels parentheses off an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;
        while (current is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized.Expression;
        }

        return current;
    }

    /// <summary>Returns the rightmost identifier of a written type name.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns>The simple name, or <see langword="null"/> when the syntax names no simple identifier.</returns>
    private static string? GetSimpleTypeName(TypeSyntax type) => type switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetSimpleTypeName(qualified.Right),
        AliasQualifiedNameSyntax alias => GetSimpleTypeName(alias.Name),
        _ => null,
    };

    /// <summary>The ASP.NET Core body-owning types resolved once per compilation.</summary>
    /// <param name="HttpRequest">The request type whose <c>Body</c> the rule watches; always present while the rule is registered.</param>
    /// <param name="HttpResponse">The response type whose <c>Body</c> the rule watches, when the framework has one.</param>
    private readonly record struct BodyGate(INamedTypeSymbol HttpRequest, INamedTypeSymbol? HttpResponse);
}
