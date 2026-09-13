// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an equality assertion whose expected and actual arguments are reversed (SST2502): a constant sits in
/// the actual (second) position while a computed value sits in the expected (first) position, as in
/// <c>Assert.Equal(result, 42)</c>. These assertions take the expected value first, so a failure printed as
/// "expected result, actual 42" blames the wrong side; a code fix swaps the two arguments.
/// </summary>
/// <remarks>
/// <para>
/// A false positive here is costly — it would recolour a passing, correct test as wrong — so the rule reports
/// only the one shape that is almost certainly a mistake: the value the test knows in advance (a literal, a
/// <c>const</c>, a <c>nameof</c>, an enum member, any compile-time constant) is in the actual slot, and a
/// computed value is in the expected slot. The common, correct shape — a constant first, a computed value
/// second — is never reported, nor is a call where both arguments are constant or both are computed.
/// </para>
/// <para>
/// Detection is per framework and strict about which position is "expected": xUnit's <c>Assert.Equal</c>, and
/// the classic <c>Assert.AreEqual</c> of the other two frameworks, all take the expected value first. The
/// actual-first fluent form is a deliberate design of one framework, not a reversal, and is never touched
/// because it does not bind to any of the targeted methods.
/// </para>
/// <para>
/// The clean path is a syntactic prepass — a two-argument, positional call whose invoked name is
/// <c>Equal</c> or <c>AreEqual</c> — and the constant shape is checked before the assertion host types
/// are resolved or the call is bound, so correct constant-first assertions are pruned without overload
/// resolution. A call is reported only when its assertion host type resolves in the compilation.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2502ReversedEqualityAssertionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property carrying the position the reported argument should move to.</summary>
    internal const string SwapWithKey = "SwapWith";

    /// <summary>XUnit's equality assertion method name.</summary>
    private const string XunitEqualName = "Equal";

    /// <summary>The classic equality assertion method name shared by the other two frameworks.</summary>
    private const string ClassicAreEqualName = "AreEqual";

    /// <summary>The number of arguments the targeted expected/actual overloads take.</summary>
    private const int EqualityArgumentCount = 2;

    /// <summary>The expected value's position — where the constant belongs.</summary>
    private const string ExpectedPosition = "0";

    /// <summary>The property bag telling the fix the reported actual argument belongs in the expected position.</summary>
    private static readonly ImmutableDictionary<string, string?> SwapProperties =
        ImmutableDictionary<string, string?>.Empty.Add(SwapWithKey, ExpectedPosition);

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(TestingRules.ReversedEqualityAssertion);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var markers = new AssertionMarkers(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, markers), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Analyzes one call for a reversed equality assertion.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="markers">The compilation's lazily resolved assertion types.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, AssertionMarkers markers)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count != EqualityArgumentCount)
        {
            return;
        }

        var name = GetInvokedName(invocation);
        if (name is not (XunitEqualName or ClassicAreEqualName))
        {
            return;
        }

        var expected = arguments[0];
        var actual = arguments[1];
        if (expected.NameColon is not null || actual.NameColon is not null)
        {
            return;
        }

        // The reversed shape: a constant in the actual slot and a computed value in the expected slot. Checking
        // this before binding prunes every correct constant-first assertion without overload resolution.
        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        if (!IsConstant(model, actual.Expression, cancellationToken) || IsConstant(model, expected.Expression, cancellationToken))
        {
            return;
        }

        if (!IsTargetedAssertionCall(context, invocation, markers))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            TestingRules.ReversedEqualityAssertion,
            actual.SyntaxTree,
            actual.Span,
            SwapProperties));
    }

    /// <summary>Binds a reversed candidate to a supported framework assertion.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="invocation">The candidate equality assertion.</param>
    /// <param name="markers">The compilation's lazily resolved assertion types.</param>
    /// <returns>Whether the call is an expected-first assertion from a supported framework.</returns>
    private static bool IsTargetedAssertionCall(in SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, AssertionMarkers markers)
    {
        var types = markers.Get();
        var xunit = types[0];
        var msTest = types[1];
        var nunit = types[AssertionMarkers.NUnitIndex];
        if (xunit is null && msTest is null && nunit is null)
        {
            return false;
        }

        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
            && IsTargetedAssertion(method, xunit, msTest, nunit);
    }

    /// <summary>Returns whether an expression is a compile-time constant.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The expression to inspect.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the expression folds to a constant value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsConstant(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellationToken) =>
        model.GetConstantValue(expression, cancellationToken).HasValue;

    /// <summary>Returns whether a bound method is one of the targeted expected-first equality assertions.</summary>
    /// <param name="method">The bound method.</param>
    /// <param name="xunit">The resolved xUnit assertion type, or <see langword="null"/>.</param>
    /// <param name="msTest">The resolved MSTest assertion type, or <see langword="null"/>.</param>
    /// <param name="nunit">The resolved NUnit assertion type, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the call is an equality assertion whose expected value comes first.</returns>
    private static bool IsTargetedAssertion(
        IMethodSymbol method,
        INamedTypeSymbol? xunit,
        INamedTypeSymbol? msTest,
        INamedTypeSymbol? nunit)
    {
        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(containingType, xunit)
            ? method.Name == XunitEqualName
            : method.Name == ClassicAreEqualName
            && (SymbolEqualityComparer.Default.Equals(containingType, msTest) || SymbolEqualityComparer.Default.Equals(containingType, nunit));
    }

    /// <summary>Returns the invoked member's simple name for the supported call shapes.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The invoked name, or <see langword="null"/> for unsupported expression shapes.</returns>
    private static string? GetInvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Resolves assertion host types on first demand and caches absent types too.</summary>
    /// <param name="compilation">The compilation whose types are resolved.</param>
    private sealed class AssertionMarkers(Compilation compilation)
    {
        /// <summary>The NUnit assertion type's position in the resolved array.</summary>
        public const int NUnitIndex = 2;

        /// <summary>The metadata name of xUnit's assertion host type.</summary>
        private const string XunitAssertMetadataName = "Xunit.Assert";

        /// <summary>The metadata name of MSTest's assertion host type.</summary>
        private const string MsTestAssertMetadataName = "Microsoft.VisualStudio.TestTools.UnitTesting.Assert";

        /// <summary>The metadata name of NUnit's assertion host type.</summary>
        private const string NUnitAssertMetadataName = "NUnit.Framework.Assert";

        /// <summary>The assertion types, or null before the first reversed candidate.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the assertion host types, resolving them on first demand.</summary>
        /// <returns>The xUnit, MSTest, and NUnit types, in that order, with null for absent types.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol?[] Get() => _resolved ??=
        [
            compilation.GetTypeByMetadataName(XunitAssertMetadataName),
            compilation.GetTypeByMetadataName(MsTestAssertMetadataName),
            compilation.GetTypeByMetadataName(NUnitAssertMetadataName),
        ];
    }
}
