// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an equality assertion whose expected operand is a boolean literal (SST2503) — <c>Assert.Equal(true, x)</c>
/// on xUnit, <c>Assert.AreEqual(true, x)</c> on MSTest and NUnit — and steers it to the framework's dedicated
/// boolean assertion (<c>Assert.True</c>/<c>Assert.False</c> or <c>Assert.IsTrue</c>/<c>Assert.IsFalse</c>). The
/// equality form obscures the intent and produces an expected-versus-actual failure instead of one that names the
/// condition. A <c>true</c> literal maps to the affirmative assertion and a <c>false</c> literal to its negation;
/// the boolean literal is recognised in either argument position.
/// </summary>
/// <remarks>
/// <para>
/// The whole rule is gated at compilation start on at least one recognised <c>Assert</c> type resolving, so a
/// project that references no test framework registers nothing. The clean path is a syntactic prepass: the invoked
/// name must be <c>Equal</c> or <c>AreEqual</c>, the call must have exactly two arguments, and one of them must be a
/// <c>true</c>/<c>false</c> literal. Only then does the rule bind — confirming the call resolves to a framework
/// <c>Assert</c> type, that the other operand is itself a boolean value, and that the target boolean assertion
/// exists — so a suggestion is never made toward a method that is not there.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2503BooleanLiteralAssertionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The xUnit equality assertion method name.</summary>
    internal const string XunitEqualityMethod = "Equal";

    /// <summary>The MSTest/NUnit equality assertion method name.</summary>
    internal const string ClassicEqualityMethod = "AreEqual";

    /// <summary>The simple type name of every recognised assertion class.</summary>
    private const string AssertTypeName = "Assert";

    /// <summary>The namespace of the xUnit assertion class.</summary>
    private const string XunitNamespace = "Xunit";

    /// <summary>The namespace of the NUnit assertion class.</summary>
    private const string NUnitNamespace = "NUnit.Framework";

    /// <summary>The namespace of the MSTest assertion class.</summary>
    private const string MSTestNamespace = "Microsoft.VisualStudio.TestTools.UnitTesting";

    /// <summary>The xUnit affirmative boolean assertion method name.</summary>
    private const string XunitTrueMethod = "True";

    /// <summary>The xUnit negative boolean assertion method name.</summary>
    private const string XunitFalseMethod = "False";

    /// <summary>The MSTest/NUnit affirmative boolean assertion method name.</summary>
    private const string ClassicTrueMethod = "IsTrue";

    /// <summary>The MSTest/NUnit negative boolean assertion method name.</summary>
    private const string ClassicFalseMethod = "IsFalse";

    /// <summary>The metadata names of the assertion classes that gate the rule.</summary>
    private static readonly string[] AssertTypeMetadataNames =
    [
        "Xunit.Assert",
        "NUnit.Framework.Assert",
        "Microsoft.VisualStudio.TestTools.UnitTesting.Assert",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(TestingRules.BooleanLiteralAssertion);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (!ReferencesAnyAssertType(start.Compilation))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns the invoked member's simple name for the supported call shapes.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns>The invoked name, or <see langword="null"/> for unsupported expression shapes.</returns>
    internal static string? GetInvokedSimpleName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Returns the index of the first argument that is a boolean literal, or <c>-1</c>.</summary>
    /// <param name="arguments">The call's argument list.</param>
    /// <returns>The zero-based index of the boolean-literal argument among the first two, or <c>-1</c>.</returns>
    internal static int GetBooleanLiteralArgumentIndex(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        var limit = arguments.Count < 2 ? arguments.Count : 2;
        for (var i = 0; i < limit; i++)
        {
            var expression = arguments[i].Expression;
            if (expression.IsKind(SyntaxKind.TrueLiteralExpression) || expression.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Resolves the boolean assertion an equality call should switch to, or <see langword="null"/>.</summary>
    /// <param name="equalityMethod">The bound equality assertion method.</param>
    /// <param name="literalIsTrue"><see langword="true"/> when the boolean literal is <c>true</c>.</param>
    /// <returns>The target boolean assertion method name, or <see langword="null"/> when none applies.</returns>
    internal static string? TryGetBooleanAssertion(IMethodSymbol equalityMethod, bool literalIsTrue)
    {
        var assertType = equalityMethod.ContainingType;
        if (assertType is not { Name: AssertTypeName })
        {
            return null;
        }

        if (GetFrameworkMethods(assertType) is not { } names || equalityMethod.Name != names.Equality)
        {
            return null;
        }

        var target = literalIsTrue ? names.True : names.False;
        return HasBooleanAssertion(assertType, target) ? target : null;
    }

    /// <summary>Maps a recognised assertion class to its equality and boolean assertion method names.</summary>
    /// <param name="assertType">The assertion class.</param>
    /// <returns>The framework's method names, or <see langword="null"/> when the namespace is unrecognised.</returns>
    private static (string Equality, string True, string False)? GetFrameworkMethods(INamedTypeSymbol assertType)
        => assertType.ContainingNamespace?.ToDisplayString() switch
        {
            XunitNamespace => (XunitEqualityMethod, XunitTrueMethod, XunitFalseMethod),
            NUnitNamespace or MSTestNamespace => (ClassicEqualityMethod, ClassicTrueMethod, ClassicFalseMethod),
            _ => null,
        };

    /// <summary>Analyzes one invocation for a boolean literal handed to an equality assertion.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        var name = GetInvokedSimpleName(invocation);
        if (name is not (XunitEqualityMethod or ClassicEqualityMethod))
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count != 2)
        {
            return;
        }

        var literalIndex = GetBooleanLiteralArgumentIndex(arguments);
        if (literalIndex < 0)
        {
            return;
        }

        var model = context.SemanticModel;
        if (model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        var actualExpression = arguments[1 - literalIndex].Expression;
        if (model.GetTypeInfo(actualExpression, context.CancellationToken).Type is not { SpecialType: SpecialType.System_Boolean })
        {
            return;
        }

        var literalIsTrue = arguments[literalIndex].Expression.IsKind(SyntaxKind.TrueLiteralExpression);
        if (TryGetBooleanAssertion(method, literalIsTrue) is not { } targetMethod)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            TestingRules.BooleanLiteralAssertion,
            GetNameLocation(invocation),
            targetMethod));
    }

    /// <summary>Returns whether the compilation references any recognised assertion type.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    /// <returns><see langword="true"/> when at least one assertion type resolves.</returns>
    private static bool ReferencesAnyAssertType(Compilation compilation)
    {
        for (var i = 0; i < AssertTypeMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(AssertTypeMetadataNames[i]) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an assertion type exposes a boolean assertion callable with a single argument.</summary>
    /// <param name="assertType">The assertion class.</param>
    /// <param name="methodName">The boolean assertion method name.</param>
    /// <returns><see langword="true"/> when a single-argument boolean overload exists.</returns>
    private static bool HasBooleanAssertion(INamedTypeSymbol assertType, string methodName)
    {
        var members = assertType.GetMembers(methodName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol method && AcceptsSingleBoolean(method))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a method binds to a single boolean argument.</summary>
    /// <param name="method">The candidate boolean assertion method.</param>
    /// <returns><see langword="true"/> when the method takes a boolean first parameter and every later one is optional.</returns>
    private static bool AcceptsSingleBoolean(IMethodSymbol method)
    {
        var parameters = method.Parameters;
        if (parameters.Length == 0 || !IsBooleanLike(parameters[0].Type))
        {
            return false;
        }

        for (var i = 1; i < parameters.Length; i++)
        {
            if (!parameters[i].IsOptional && !parameters[i].IsParams)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a type is <see cref="bool"/> or <see cref="Nullable{Boolean}"/>.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type accepts a boolean argument.</returns>
    private static bool IsBooleanLike(ITypeSymbol type)
        => type.SpecialType == SpecialType.System_Boolean
            || type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments: [{ SpecialType: SpecialType.System_Boolean }],
            };

    /// <summary>Returns the location of the invoked assertion method's name.</summary>
    /// <param name="invocation">The reported invocation.</param>
    /// <returns>The name's location, or the whole invocation's when it has no simple name.</returns>
    private static Location GetNameLocation(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.GetLocation(),
        MemberBindingExpressionSyntax binding => binding.Name.GetLocation(),
        SimpleNameSyntax simple => simple.GetLocation(),
        _ => invocation.GetLocation(),
    };
}
