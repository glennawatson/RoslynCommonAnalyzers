// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a stream read whose returned byte count is awaited and thrown away (SST2446), in the two shapes
/// that keep the count out of sight: <c>await stream.ReadAsync(buffer).ConfigureAwait(false);</c>, and a read
/// stored in a local that is then awaited as a statement. A single read may fill less of the buffer than was
/// asked for, so discarding the count silently accepts a partial read.
/// </summary>
/// <remarks>
/// <para>
/// The rule reports only these two shapes on purpose: a bare <c>await stream.ReadAsync(buffer);</c> and the
/// synchronous reads are a different, already-guarded shape, and reporting them here would double up. Every
/// read whose count is assigned, returned, or compared is correct by construction and never reported.
/// </para>
/// <para>
/// The message and the fix adapt to the framework. Where the read-exactly API resolves, the message names it
/// and a code fix rewrites the configured-await shape to it; where it does not — on the older frameworks a
/// library still targets — the defect is just as real, so the read is still reported but the message steers to
/// a read loop and no fix is offered rather than one that will not compile.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on <c>System.IO.Stream</c> resolving. The clean path is a
/// syntactic prepass: the await must be an expression statement (its result discarded), and after unwrapping a
/// configured awaiter the read must be a <c>ReadAsync</c> call reached through that awaiter or a local. Nothing
/// binds until that holds.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2446DiscardedStreamReadAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The read method whose count is being discarded.</summary>
    internal const string ReadAsyncName = "ReadAsync";

    /// <summary>The read-exactly method the fix steers toward.</summary>
    internal const string ReadExactlyAsyncName = "ReadExactlyAsync";

    /// <summary>The configured-awaiter method that hides the discarded read.</summary>
    private const string ConfigureAwaitName = "ConfigureAwait";

    /// <summary>The metadata name of the stream type.</summary>
    private const string StreamMetadataName = "System.IO.Stream";

    /// <summary>The suggestion appended when the read-exactly API is available.</summary>
    private const string ReadExactlySuggestion = "read the buffer fully with 'ReadExactlyAsync', or act on the returned count";

    /// <summary>The suggestion appended when the read-exactly API is not available.</summary>
    private const string LoopSuggestion = "loop until the buffer is filled, or act on the returned count";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.DiscardedStreamRead);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(StreamMetadataName) is not { } streamType)
            {
                return;
            }

            var suggestion = HasReadExactly(streamType) ? ReadExactlySuggestion : LoopSuggestion;
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAwait(nodeContext, streamType, suggestion), SyntaxKind.AwaitExpression);
        });
    }

    /// <summary>Returns the read invocation an await ultimately discards, or <see langword="null"/> before binding.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="awaitExpression">The await expression to inspect.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The reported <c>ReadAsync</c> invocation, or <see langword="null"/> when the shape does not match.</returns>
    internal static InvocationExpressionSyntax? TryGetDiscardedRead(SemanticModel model, AwaitExpressionSyntax awaitExpression, CancellationToken cancellationToken)
    {
        if (awaitExpression.Parent is not ExpressionStatementSyntax)
        {
            return null;
        }

        var operand = Unwrap(awaitExpression.Expression);
        var viaConfigureAwait = TryUnwrapConfigureAwait(operand, out var inner);
        if (viaConfigureAwait)
        {
            operand = Unwrap(inner!);
        }

        return operand switch
        {
            InvocationExpressionSyntax invocation when GetInvokedName(invocation) == ReadAsyncName
                => viaConfigureAwait ? invocation : null,
            IdentifierNameSyntax identifier => ResolveLocalRead(model, identifier, cancellationToken),
            _ => null,
        };
    }

    /// <summary>Returns the invoked member's simple name text for the supported call shapes.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The invoked name, or <see langword="null"/> for unsupported expression shapes.</returns>
    internal static string? GetInvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Analyzes one await for a discarded stream read.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="streamType">The compilation's stream type.</param>
    /// <param name="suggestion">The compilation-specific replacement advice.</param>
    private static void AnalyzeAwait(SyntaxNodeAnalysisContext context, INamedTypeSymbol streamType, string suggestion)
    {
        var awaitExpression = (AwaitExpressionSyntax)context.Node;
        if (TryGetDiscardedRead(context.SemanticModel, awaitExpression, context.CancellationToken) is not { } readInvocation)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(readInvocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: ReadAsyncName } method
            || !IsStreamOrDerived(method.ContainingType, streamType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.DiscardedStreamRead,
            GetReadLocation(readInvocation),
            suggestion));
    }

    /// <summary>Returns the location of the invoked read method's name.</summary>
    /// <param name="invocation">The read invocation.</param>
    /// <returns>The name's location, or the whole invocation's when it has no simple name.</returns>
    private static Location GetReadLocation(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.GetLocation(),
        MemberBindingExpressionSyntax binding => binding.Name.GetLocation(),
        SimpleNameSyntax simple => simple.GetLocation(),
        _ => invocation.GetLocation(),
    };

    /// <summary>Resolves a bare identifier to the read invocation that initialized its local.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="identifier">The awaited identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The read invocation, or <see langword="null"/> when the local was not initialized with one.</returns>
    private static InvocationExpressionSyntax? ResolveLocalRead(SemanticModel model, IdentifierNameSyntax identifier, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(identifier, cancellationToken).Symbol is not ILocalSymbol local
            || local.DeclaringSyntaxReferences.Length != 1
            || local.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is not VariableDeclaratorSyntax { Initializer.Value: { } value })
        {
            return null;
        }

        return Unwrap(value) is InvocationExpressionSyntax invocation && GetInvokedName(invocation) == ReadAsyncName
            ? invocation
            : null;
    }

    /// <summary>Peels parentheses off an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>Returns whether an expression is <c>x.ConfigureAwait(...)</c> and yields <c>x</c>.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="inner">The configured awaiter's receiver when the shape matches.</param>
    /// <returns><see langword="true"/> when the expression is a configured awaiter.</returns>
    private static bool TryUnwrapConfigureAwait(ExpressionSyntax expression, out ExpressionSyntax? inner)
    {
        if (expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: ConfigureAwaitName } access })
        {
            inner = access.Expression;
            return true;
        }

        inner = null;
        return false;
    }

    /// <summary>Returns whether a type is the stream type or derives from it.</summary>
    /// <param name="type">The method's containing type.</param>
    /// <param name="streamType">The compilation's stream type.</param>
    /// <returns><see langword="true"/> when the read belongs to a stream.</returns>
    private static bool IsStreamOrDerived(INamedTypeSymbol type, INamedTypeSymbol streamType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, streamType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the stream type exposes the read-exactly API (.NET 7 and later).</summary>
    /// <param name="streamType">The compilation's stream type.</param>
    /// <returns><see langword="true"/> when the read-exactly method exists.</returns>
    private static bool HasReadExactly(INamedTypeSymbol streamType)
    {
        var members = streamType.GetMembers(ReadExactlyAsyncName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol)
            {
                return true;
            }
        }

        return false;
    }
}
