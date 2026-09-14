// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyEqualsOverCompare = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1216UseEqualsOverCompareAnalyzer,
    PerformanceSharp.Analyzers.Psh1216UseEqualsOverCompareCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1216 (ask for equality, not ordering) and its code fix.</summary>
public class UseEqualsOverCompareAnalyzerUnitTest
{
    /// <summary>Verifies <c>string.Compare(a, b) == 0</c> is rewritten to a current-culture <c>string.Equals</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TwoArgumentCompareBecomesCurrentCultureEqualsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:string.Compare(left, right) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.CurrentCulture);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the reversed <c>0 == string.Compare(a, b)</c> operand order is rewritten the same way.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReversedCompareBecomesCurrentCultureEqualsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:0 == string.Compare(left, right)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.CurrentCulture);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies <c>string.Compare(a, b) != 0</c> is rewritten to a negated <c>string.Equals</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareInequalityBecomesNegatedEqualsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:string.Compare(left, right) != 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => !string.Equals(left, right, System.StringComparison.CurrentCulture);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an explicit <c>StringComparison</c> argument is carried over unchanged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareWithExplicitComparisonKeepsComparisonAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:string.Compare(left, right, System.StringComparison.OrdinalIgnoreCase) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a literal <c>true</c> ignore-case flag maps to <c>CurrentCultureIgnoreCase</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareWithTrueFlagBecomesCurrentCultureIgnoreCaseAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:string.Compare(left, right, true) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.CurrentCultureIgnoreCase);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a literal <c>false</c> ignore-case flag maps to <c>CurrentCulture</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareWithFalseFlagBecomesCurrentCultureAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:string.Compare(left, right, false) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.CurrentCulture);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies <c>string.CompareOrdinal(a, b) == 0</c> maps to an ordinal <c>string.Equals</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareOrdinalBecomesOrdinalEqualsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:string.CompareOrdinal(left, right) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.Ordinal);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies instance <c>a.CompareTo(b) == 0</c> on strings maps to a current-culture <c>string.Equals</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareToBecomesCurrentCultureEqualsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:left.CompareTo(right) == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => string.Equals(left, right, System.StringComparison.CurrentCulture);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies instance <c>a.CompareTo(b) != 0</c> on strings is rewritten to a negated <c>string.Equals</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompareToInequalityBecomesNegatedEqualsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => {|PSH1216:left.CompareTo(right) != 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string left, string right)
                                           => !string.Equals(left, right, System.StringComparison.CurrentCulture);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a real ordering test (<c>&gt; 0</c>) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrderingComparisonIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right)
                                      => string.Compare(left, right) > 0;
                              }
                              """;
        await VerifyNet90CleanAsync(Source);
    }

    /// <summary>Verifies <c>CompareTo</c> on a non-string receiver is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonStringCompareToIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(int left, int right)
                                      => left.CompareTo(right) == 0;
                              }
                              """;
        await VerifyNet90CleanAsync(Source);
    }

    /// <summary>Verifies the <c>ignoreCase</c> overload with a non-literal flag stays silent — the flag's
    /// value is unknown, so no <c>StringComparison</c> mapping would be safe.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralIgnoreCaseFlagIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string left, string right, bool ignoreCase)
                                      => string.Compare(left, right, ignoreCase) == 0;
                              }
                              """;
        await VerifyNet90CleanAsync(Source);
    }

    /// <summary>Verifies only bare integer zero and recognized member call shapes are candidates.</summary>
    /// <param name="expression">The equality expression to classify.</param>
    /// <param name="expectedName">The recognized method name, or null.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string.Compare(a, b) == 0", "Compare")]
    [Arguments("0 != string.CompareOrdinal(a, b)", "CompareOrdinal")]
    [Arguments("a.CompareTo(b) == 0", "CompareTo")]
    [Arguments("string.Compare(a, b, option) == 0", "Compare")]
    [Arguments("string.Compare(a, b) == 1", null)]
    [Arguments("string.Compare(a, b) == 0L", null)]
    [Arguments("string.Compare(a, b) == (0)", null)]
    [Arguments("string.Compare(a, b) == false", null)]
    [Arguments("value == 0", null)]
    [Arguments("Compare(a, b) == 0", null)]
    [Arguments("(string.Compare(a, b)) == 0", null)]
    [Arguments("value?.CompareTo(a) == 0", null)]
    [Arguments("value->Compare(a, b) == 0", null)]
    [Arguments("value.Compare<int>(a, b) == 0", null)]
    [Arguments("value.Other(a, b) == 0", null)]
    [Arguments("value.Compare(a) == 0", null)]
    [Arguments("value.Compare(a, b, c, d) == 0", null)]
    [Arguments("value.CompareOrdinal(a) == 0", null)]
    [Arguments("value.CompareTo() == 0", null)]
    [Arguments("0 == value", null)]
    public async Task OrderingSyntaxRequiresExactShapeAsync(string expression, string? expectedName)
    {
        var binary = (BinaryExpressionSyntax)SyntaxFactory.ParseExpression(expression);
        var matches = Psh1216UseEqualsOverCompareAnalyzer.TryGetOrderingCall(binary, out var invocation, out var methodName);
        await Assert.That(matches).IsEqualTo(expectedName is not null);
        await Assert.That(methodName).IsEqualTo(expectedName);
        await Assert.That(invocation is not null).IsEqualTo(matches);
    }

    /// <summary>Verifies unresolved calls and non-string comparison overloads stay silent.</summary>
    /// <param name="expression">The comparison in a runtime-backed compilation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Missing.Compare(left, right) == 0")]
    [Arguments("left.CompareTo((object)right) == 0")]
    [Arguments("string.Compare(left, right, (true)) == 0")]
    [Arguments("string.Compare(left, right) == 1")]
    [Arguments("left.Length == 0")]
    public async Task UnrewritableRuntimeComparisonIsCleanAsync(string expression)
    {
        var source = $"class C {{ bool M(string left, string right) => {expression}; }}";
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1216UseEqualsOverCompareAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies the cached comparison enum is reused for subsequent matching calls.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MultipleComparisonsInOneCompilationReportAsync()
    {
        const int ExpectedCount = 2;
        const string Source = "class C { bool M(string a, string b) => string.Compare(a, b) == 0 || 0 != string.CompareOrdinal(a, b); }";
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(Source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1216UseEqualsOverCompareAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(ExpectedCount);
    }

    /// <summary>Verifies alternate framework overloads must preserve the supported string parameter contract.</summary>
    /// <param name="member">The framework ordering declaration.</param>
    /// <param name="expression">The call against that declaration.</param>
    /// <param name="hasComparisonType">Whether the replacement comparison enum exists.</param>
    /// <param name="expectedCount">The expected diagnostic count.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public static int Compare(string a, string b) => 0;", "string.Compare(a, b) == 0", false, 0)]
    [Arguments("public static int Compare(string a, string b) => 0;", "string.Compare(a, b) == 0", true, 1)]
    [Arguments("public int Compare(string a, string b) => 0;", "a.Compare(a, b) == 0", true, 0)]
    [Arguments("public static int Compare(object a, string b) => 0;", "string.Compare(a, b) == 0", true, 0)]
    [Arguments("public static int Compare(string a, object b) => 0;", "string.Compare(a, b) == 0", true, 0)]
    [Arguments("public static int Compare(params string[] values) => 0;", "string.Compare(a, b) == 0", true, 0)]
    [Arguments("public static int Compare(string a, string b, int x = 0, int y = 0) => 0;", "string.Compare(a, b) == 0", true, 0)]
    [Arguments("public static int Compare(string a, string b, int x) => 0;", "string.Compare(a, b, 1) == 0", true, 0)]
    [Arguments("public int CompareOrdinal(string a, string b) => 0;", "a.CompareOrdinal(a, b) == 0", true, 0)]
    [Arguments("public static int CompareOrdinal(string a, object b) => 0;", "string.CompareOrdinal(a, b) == 0", true, 0)]
    [Arguments("public static int CompareTo(string b) => 0;", "string.CompareTo(b) == 0", true, 0)]
    [Arguments("public int CompareTo(string b, int x = 0) => 0;", "a.CompareTo(b) == 0", true, 0)]
    public async Task FrameworkOrderingOverloadRequiresSupportedContractAsync(string member, string expression, bool hasComparisonType, int expectedCount)
    {
        var comparison = hasComparisonType ? "public enum StringComparison { CurrentCulture }" : string.Empty;
        var source = $$"""
                       namespace System
                       {
                           public class Object { }
                           public class ValueType { }
                           public struct Void { }
                           public struct Int32 { }
                           public struct Boolean { }
                           public class Enum : ValueType { }
                           public class Attribute { }
                           public class ParamArrayAttribute : Attribute { }
                           public sealed class String { {{member}} }
                           {{comparison}}
                       }
                       class C { bool M(string a, string b) => {{expression}}; }
                       """;
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(source)]);
        var stringType = compilation.GetSpecialType(SpecialType.System_String);
        await Assert.That(stringType.SpecialType).IsEqualTo(SpecialType.System_String);
        var tree = compilation.SyntaxTrees[0];
        var root = await tree.GetRootAsync();
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        await Assert.That(compilation.GetSemanticModel(tree).GetSymbolInfo(invocation).Symbol is IMethodSymbol).IsTrue();
        var diagnostics = await compilation.WithAnalyzers([new Psh1216UseEqualsOverCompareAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(expectedCount);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyEqualsOverCompare.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task VerifyNet90CleanAsync(string source) =>
        VerifyNet90Async(source, source);
}
