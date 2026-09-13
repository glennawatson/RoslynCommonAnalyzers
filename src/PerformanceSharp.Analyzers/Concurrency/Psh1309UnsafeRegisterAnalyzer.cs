// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>CancellationToken.Register</c> calls that pass an explicit state argument
/// (PSH1309), where <c>UnsafeRegister</c> runs the same callback without capturing and
/// restoring the <c>ExecutionContext</c>. Only the two-argument overloads are reported
/// because only they have an UnsafeRegister twin with the same signature. The rule is
/// opt-in — skipping the context capture changes what AsyncLocal state the callback sees —
/// and gated on <c>UnsafeRegister</c> existing in the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1309UnsafeRegisterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the syntax gate requires.</summary>
    internal const string RegisterMethodName = "Register";

    /// <summary>The replacement member name the fix moves to.</summary>
    internal const string UnsafeRegisterMethodName = "UnsafeRegister";

    /// <summary>The argument count the reported overloads carry.</summary>
    internal const int CallbackAndStateArgumentCount = 2;

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ConcurrencyRules.UseUnsafeRegister);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var tokenType = new CancellationTokenType(start.Compilation);

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, tokenType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation has the two-argument <c>.Register(callback, state)</c> shape, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the member name is Register with two arguments.</returns>
    internal static bool IsRegisterShape(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Count == CallbackAndStateArgumentCount
            && invocation.Expression is MemberAccessExpressionSyntax access
            && access.Name.Identifier.ValueText == RegisterMethodName;

    /// <summary>Reports PSH1309 for a Register overload whose UnsafeRegister twin exists.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="tokenType">The lazily resolved cancellation token type.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, CancellationTokenType tokenType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsRegisterShape(invocation)
            || tokenType.Get() is not { } resolvedTokenType
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.Name != RegisterMethodName
            || method.Parameters.Length != CallbackAndStateArgumentCount
            || method.Parameters[1].Type.SpecialType != SpecialType.System_Object
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, resolvedTokenType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.UseUnsafeRegister,
            invocation.SyntaxTree,
            invocation.Span));
    }

    /// <summary>Resolves cancellation-token support only after a two-argument Register call is found.</summary>
    /// <param name="compilation">The compilation whose cancellation token type is cached.</param>
    private sealed class CancellationTokenType(Compilation compilation)
    {
        /// <summary>The metadata name of the cancellation token type.</summary>
        private const string CancellationTokenMetadataName = "System.Threading.CancellationToken";

        /// <summary>The supported token type, or an empty array when UnsafeRegister is unavailable.</summary>
        private INamedTypeSymbol[]? _resolved;

        /// <summary>Gets the cancellation token type when it supports UnsafeRegister.</summary>
        /// <returns>The supported token type, or null when it is unavailable.</returns>
        public INamedTypeSymbol? Get()
        {
            var resolved = _resolved ??= compilation.GetTypeByMetadataName(CancellationTokenMetadataName) is { } type
                && !type.GetMembers(UnsafeRegisterMethodName).IsEmpty ? [type] : [];
            return resolved.Length == 0 ? null : resolved[0];
        }
    }
}
