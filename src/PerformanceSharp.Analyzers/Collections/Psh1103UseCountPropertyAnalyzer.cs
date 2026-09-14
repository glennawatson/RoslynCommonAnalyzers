// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests reading a collection's own <c>Count</c>/<c>Length</c> property instead of
/// calling the parameterless <c>System.Linq.Enumerable</c> <c>Count()</c> and <c>Any()</c>
/// extensions (PSH1103). An invocation qualifies only when it is the member-access
/// extension form with an empty argument list, binds to a source-only
/// <c>System.Linq.Enumerable</c> method, and the receiver's static type exposes an
/// accessible constant-time <see cref="int"/> count — directly on the type or a base
/// type, or via <c>ICollection&lt;T&gt;</c>/<c>IReadOnlyCollection&lt;T&gt;</c> for
/// interface and type-parameter receivers. The rule resolves <c>System.Linq.Enumerable</c>
/// on first demand per compilation, after a candidate has passed the syntax and receiver checks.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1103UseCountPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key carrying the suggested count property name for the code fix.</summary>
    internal const string PropertyNameKey = "PropertyName";

    /// <summary>The metadata name of the LINQ extension-method host type.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <summary>Cached diagnostic properties suggesting the Count property.</summary>
    private static readonly ImmutableDictionary<string, string?> CountProperties =
        ImmutableDictionary<string, string?>.Empty.Add(PropertyNameKey, CollectionReceiverHelper.CountPropertyName);

    /// <summary>Cached diagnostic properties suggesting the Length property.</summary>
    private static readonly ImmutableDictionary<string, string?> LengthProperties =
        ImmutableDictionary<string, string?>.Empty.Add(PropertyNameKey, CollectionReceiverHelper.LengthPropertyName);

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CollectionRules.UseCountProperty);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, EnumerableMetadataName),
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports PSH1103 for a parameterless Enumerable Count/Any call whose receiver has a constant-time count.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The deferred <c>System.Linq.Enumerable</c> type.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyMetadataType types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (TryGetCountAccess(invocation) is not { } memberAccess)
        {
            return;
        }

        // The receiver's type comes first on purpose. Typing an expression is far less work than resolving
        // an extension-method call, which searches every namespace in scope for candidate containers and
        // runs type inference; a receiver with no constant-time count rules the call out before any of that.
        if (context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type is not { } receiverType
            || !CollectionReceiverHelper.TryGetCountSourceName(receiverType, out var propertyName))
        {
            return;
        }

        if (types.Get() is not { } enumerableType
            || !EnumerableInvocationHelper.IsSourceOnlyExtensionOn(context.SemanticModel, invocation, enumerableType, context.CancellationToken))
        {
            return;
        }

        var properties = propertyName == CollectionReceiverHelper.LengthPropertyName ? LengthProperties : CountProperties;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseCountProperty,
            invocation.SyntaxTree,
            invocation.Span,
            properties,
            propertyName));
    }

    /// <summary>Gets a parameterless Count or Any member access using syntax alone.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The member access, or null when the syntax cannot match.</returns>
    private static MemberAccessExpressionSyntax? TryGetCountAccess(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && memberAccess.Name.Identifier.ValueText is "Count" or "Any"
            ? memberAccess
            : null;
}
