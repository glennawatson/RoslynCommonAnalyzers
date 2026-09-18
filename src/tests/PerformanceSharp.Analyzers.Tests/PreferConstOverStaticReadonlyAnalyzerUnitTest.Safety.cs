// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;

using VerifyPreferConst = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1402PreferConstOverStaticReadonlyAnalyzer,
    PerformanceSharp.Analyzers.Psh1402PreferConstOverStaticReadonlyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1402 (prefer const over static readonly) and its fix.</summary>
public partial class PreferConstOverStaticReadonlyAnalyzerUnitTest
{
    /// <summary>Verifies loop patterns are not made constant when that introduces an always-matching warning.</summary>
    /// <param name="doLoop">Whether to place the condition after the loop body.</param>
    /// <param name="type">The local's type.</param>
    /// <param name="initialValue">The local's constant initializer.</param>
    /// <param name="pattern">The pattern in the continuation condition.</param>
    /// <param name="warningId">The warning introduced by a const declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false, "int", "0", "< 5", "CS8793")]
    [Arguments(true, "int", "0", "< 5", "CS8793")]
    [Arguments(false, "bool", "true", "true", "CS8520")]
    [Arguments(true, "bool", "true", "true", "CS8520")]
    public async Task ConstantLoopPatternIsCleanAsync(bool doLoop, string type, string initialValue, string pattern, string warningId)
    {
        var loop = doLoop
            ? $"do {{ if (stop()) return 1; }} while (value is {pattern}); return 0;"
            : $"while (value is {pattern}) {{ if (stop()) break; }} return 0;";
        var source = $$"""
            public class C
            {
                public int Run(System.Func<bool> stop)
                {
                    {{type}} value = {{initialValue}};
                    {{loop}}
                }
            }
            """;
        var constantSource = $$"""
            public class C
            {
                public int Run(System.Func<bool> stop)
                {
                    const {{type}} value = {{initialValue}};
                    {{loop}}
                }
            }
            """;
        var compilation = CreateCompilation(source);
        await Assert.That(compilation.GetDiagnostics()).IsEmpty();
        var warnings = CreateCompilation(constantSource).GetDiagnostics().Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning).ToArray();
        await Assert.That(warnings.Length).IsEqualTo(1);
        await Assert.That(warnings[0].Id).IsEqualTo(warningId);
        var diagnostics = await compilation.WithAnalyzers([new Psh1402PreferConstOverStaticReadonlyAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a retry local is not made constant when that makes the trailing return unreachable.</summary>
    /// <param name="modifier">Whether the comparison source declares the local constant.</param>
    /// <param name="warningCount">The expected unreachable-code warning count.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", 0)]
    [Arguments("const ", 1)]
    public async Task RetryLoopConstantChangesReachabilityAsync(string modifier, int warningCount)
    {
        var source = $$"""
            public class C
            {
                public object GetTable(System.Func<object> buildTable)
                {
                    {{modifier}}int retries = 0;
                    do
                    {
                        try { return buildTable(); }
                        catch (System.Exception) { System.Threading.Thread.Sleep(100); }
                    }
                    while (retries < 5);
                    return null;
                }
            }
            """;
        var compilation = CreateCompilation(source);
        var warnings = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning).ToArray();
        await Assert.That(warnings.Length).IsEqualTo(warningCount);
        foreach (var warning in warnings)
        {
            await Assert.That(warning.Id).IsEqualTo("CS0162");
        }

        var diagnostics = await compilation.WithAnalyzers([new Psh1402PreferConstOverStaticReadonlyAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies an integer local remains mutable when const changes the selected loop-condition overload.</summary>
    /// <param name="modifier">Whether the comparison source declares the local constant.</param>
    /// <param name="parameterType">The parameter type of the selected overload.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", SpecialType.System_Int64)]
    [Arguments("const ", SpecialType.System_Byte)]
    public async Task LoopConditionConstantChangesOverloadAsync(string modifier, SpecialType parameterType)
    {
        var source = $$"""
            public static class C
            {
                public static int Run()
                {
                    {{modifier}}int retries = 0;
                    while (Continue(retries)) { return 1; }
                    return 0;
                }

                private static bool Continue(long value) => true;
                private static bool Continue(byte value) => false;
            }
            """;
        var compilation = CreateCompilation(source);
        await Assert.That(compilation.GetDiagnostics()).IsEmpty();
        var tree = compilation.SyntaxTrees.Single();
        var invocation = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var method = (IMethodSymbol)compilation.GetSemanticModel(tree).GetSymbolInfo(invocation).Symbol!;
        await Assert.That(method.Parameters[0].Type.SpecialType).IsEqualTo(parameterType);
        await using var image = new MemoryStream();
        var emitted = compilation.Emit(image);
        await Assert.That(emitted.Success).IsTrue();

        var diagnostics = await compilation.WithAnalyzers([new Psh1402PreferConstOverStaticReadonlyAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a loop continuation is not turned into a constant expression.</summary>
    /// <param name="loop">The loop using the unchanged local.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("do { if (stop()) break; } while (retries < 5);")]
    [Arguments("while (retries < 5) { if (stop()) break; }")]
    [Arguments("for (; retries < 5;) { if (stop()) break; }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnchangedLoopControlIsCleanAsync(string loop) =>
        VerifyPreferConst.VerifyAnalyzerAsync(
            $$"""
            public class C
            {
                public void Run(System.Func<bool> stop)
                {
                    var retries = 0;
                    {{loop}}
                }
            }
            """);

    /// <summary>Verifies constant loop bounds remain eligible when the continuation also reads advancing state.</summary>
    /// <param name="counterDeclaration">Whether the advancing counter is a local or a parameter.</param>
    /// <param name="loop">The loop using the invariant bound.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RuntimeLoopWithConstantBoundIsFixedAsync(
        [Matrix("", "int attempt = 0;")] string counterDeclaration,
        [Matrix("while (attempt < limit) { attempt++; }", "do { attempt++; } while (attempt < limit);", "for (; attempt < limit; attempt++) { }")] string loop) =>
        VerifyPreferConst.VerifyCodeFixAsync(
            $$"""
            public class C
            {
                public void Run({{(counterDeclaration.Length == 0 ? "int attempt" : string.Empty)}})
                {
                    {{counterDeclaration}}
                    int {|PSH1402:limit|} = 5;
                    {{loop}}
                }
            }
            """,
            $$"""
            public class C
            {
                public void Run({{(counterDeclaration.Length == 0 ? "int attempt" : string.Empty)}})
                {
                    {{counterDeclaration}}
                    const int limit = 5;
                    {{loop}}
                }
            }
            """);

    /// <summary>Verifies const-sensitive numeric conversions stay unchanged in calls and construction.</summary>
    /// <param name="expression">The use whose numeric overload can change.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Select(retries)")]
    [Arguments("Select((retries))")]
    [Arguments("Select((int)retries)")]
    [Arguments("Select(checked(retries + 0))")]
    [Arguments("Select(retries + 0)")]
    [Arguments("new C(retries).Value")]
    [Arguments("retries == new C(1L)")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NumericOverloadConversionsAreCleanAsync(string expression) =>
        VerifyPreferConst.VerifyAnalyzerAsync($$"""
            public class C
            {
                public C(long value) { }
                public C(byte value) { }
                public bool Value => true;
                public static bool Run()
                {
                    int retries = 0;
                    return {{expression}};
                }
                private static bool Select(long value) => true;
                private static bool Select(byte value) => false;
                public static bool operator ==(long value, C other) => true;
                public static bool operator !=(long value, C other) => false;
                public static bool operator ==(byte value, C other) => false;
                public static bool operator !=(byte value, C other) => true;
                public override bool Equals(object other) => ReferenceEquals(this, other);
                public override int GetHashCode() => base.GetHashCode();
            }
            """);

    /// <summary>Verifies const arithmetic cannot introduce overflow or division-by-zero compilation errors.</summary>
    /// <param name="initialValue">The unchanged local's initial value.</param>
    /// <param name="expression">The arithmetic expression whose constant evaluation is invalid.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0, "retries + int.MaxValue + 1")]
    [Arguments(1, "int.MinValue - retries")]
    [Arguments(2, "retries * int.MaxValue")]
    [Arguments(0, "1 / retries")]
    [Arguments(0, "1 % retries")]
    [Arguments(int.MinValue, "-retries")]
    [Arguments(int.MinValue, "retries / -1")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InvalidConstantArithmeticIsCleanAsync(int initialValue, string expression) =>
        VerifyPreferConst.VerifyAnalyzerAsync($$"""
            public class C
            {
                public int Run()
                {
                    int retries = {{initialValue}};
                    return {{expression}};
                }
            }
            """);

    /// <summary>Verifies safe integer arithmetic remains eligible across arithmetic and bitwise operators.</summary>
    /// <param name="expression">The arithmetic use of a constant candidate.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value + 1")]
    [Arguments("value - 1")]
    [Arguments("value * 2")]
    [Arguments("value / 2")]
    [Arguments("value % 2")]
    [Arguments("value & 2")]
    [Arguments("value | 2")]
    [Arguments("value ^ 2")]
    [Arguments("value << 1")]
    [Arguments("value >> 1")]
    [Arguments("value >>> 1")]
    [Arguments("+value")]
    [Arguments("-value")]
    [Arguments("~value")]
    [Arguments("(value + 1) * 2")]
    [Arguments("checked(value + 1) * 2")]
    [Arguments("unchecked(value + 1) * 2")]
    [Arguments("value + (int)1")]
    [Arguments("value + default(int)")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SafeIntegerArithmeticIsFixedAsync(string expression) =>
        VerifyPreferConst.VerifyCodeFixAsync(
            $$"""
            public class C
            {
                public int Run()
                {
                    int {|PSH1402:value|} = 3;
                    return {{expression}};
                }
            }
            """,
            $$"""
            public class C
            {
                public int Run()
                {
                    const int value = 3;
                    return {{expression}};
                }
            }
            """);

    /// <summary>Verifies a constant candidate cannot make an if or conditional branch constant.</summary>
    /// <param name="statement">The branch containing the candidate read.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("if (value > 0) return 1; return 0;")]
    [Arguments("return value > 0 ? 1 : 2;")]
    [Arguments("while ((true ? value : 4) > 0) { return 1; } return 0;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConstantBranchesAreCleanAsync(string statement) =>
        VerifyPreferConst.VerifyAnalyzerAsync($$"""
            public class C
            {
                public int Run()
                {
                    int value = 3;
                    {{statement}}
                }
            }
            """);

    /// <summary>Verifies conditional arithmetic with runtime input retains a safe constant local.</summary>
    /// <param name="expression">The expression that retains runtime-dependent operands.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("(gate ? value : 4) + runtime")]
    [Arguments("(true ? runtime : value) + runtime")]
    [Arguments("(true ? value : runtime) + runtime")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RuntimeConditionalArithmeticIsFixedAsync(string expression) =>
        VerifyPreferConst.VerifyCodeFixAsync(
            $$"""
            public class C
            {
                public int Run(int runtime, bool gate)
                {
                    int {|PSH1402:value|} = 3;
                    return {{expression}};
                }
            }
            """,
            $$"""
            public class C
            {
                public int Run(int runtime, bool gate)
                {
                    const int value = 3;
                    return {{expression}};
                }
            }
            """);

    /// <summary>Verifies dynamically bound invocations and indexers retain the argument's variable semantics.</summary>
    /// <param name="expression">The dynamic call that consumes the candidate.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("receiver.Select(value)")]
    [Arguments("receiver[value]")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DynamicArgumentsAreCleanAsync(string expression) =>
        VerifyPreferConst.VerifyAnalyzerAsync($$"""
            public class C
            {
                public int Run(dynamic receiver)
                {
                    int value = 3;
                    return {{expression}};
                }
            }
            """);

    /// <summary>Verifies arithmetic outside the bounded Int32 evaluator remains unchanged.</summary>
    /// <param name="type">The arithmetic type.</param>
    /// <param name="initializer">The value used by the arithmetic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("long", "3L")]
    [Arguments("uint", "3U")]
    [Arguments("ulong", "3UL")]
    [Arguments("decimal", "3M")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task WiderArithmeticIsCleanAsync(string type, string initializer) =>
        VerifyPreferConst.VerifyAnalyzerAsync($$"""
            public class C
            {
                public {{type}} Run()
                {
                    {{type}} value = {{initializer}};
                    return value + 1;
                }
            }
            """);

    /// <summary>Verifies the arithmetic helper declines runtime inputs, mutation, nullable selection, and noninteger expressions.</summary>
    /// <param name="returnType">The expression's enclosing return type.</param>
    /// <param name="declarations">The values in scope for the expression.</param>
    /// <param name="expression">The expression whose constant value cannot be proved by the helper.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int", "int value = 3;", "runtime + value")]
    [Arguments("int", "int value = 3;", "Fetch() + value")]
    [Arguments("int", "int value = 3;", "++value")]
    [Arguments("int", "int value = runtime;", "value + 1")]
    [Arguments("int", "int value; value = runtime;", "value + 1")]
    [Arguments("int", "int? optional = 3; int value = 3;", "optional ?? value")]
    [Arguments("int", "int? optional = null; int value = 3;", "optional ?? value")]
    [Arguments("System.Func<int>", "", "Fetch")]
    [Arguments("long", "", "3L")]
    public async Task UnprovedArithmeticIsRejectedAsync(string returnType, string declarations, string expression)
    {
        var source = $$"""
            public class C
            {
                public {{returnType}} Run(int runtime)
                {
                    {{declarations}}
                    return {{expression}};
                }
                private int Fetch() => System.Environment.TickCount;
            }
            """;
        var compilation = CreateCompilation(source);
        await Assert.That(compilation.GetDiagnostics()).IsEmpty();
        var tree = compilation.SyntaxTrees.Single();
        var returned = (await tree.GetRootAsync()).DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression!;
        await Assert.That(ConstInt32Arithmetic.IsSafe(returned, compilation.GetSemanticModel(tree), CancellationToken.None)).IsFalse();
    }

    /// <summary>Verifies constant narrow integers retain their ordinary arithmetic promotion.</summary>
    /// <param name="type">The integral operand type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("sbyte")]
    [Arguments("byte")]
    [Arguments("short")]
    [Arguments("ushort")]
    [Arguments("char")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NarrowIntegerArithmeticIsFixedAsync(string type) =>
        VerifyPreferConst.VerifyCodeFixAsync(
            $$"""
            public class C
            {
                private const {{type}} Seed = ({{type}})3;
                public int Run()
                {
                    {{type}} {|PSH1402:value|} = Seed;
                    return value + 1;
                }
            }
            """,
            $$"""
            public class C
            {
                private const {{type}} Seed = ({{type}})3;
                public int Run()
                {
                    const {{type}} value = Seed;
                    return value + 1;
                }
            }
            """);

    /// <summary>Verifies the arithmetic scan conservatively declines deeply nested constant expressions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeepConstantArithmeticIsCleanAsync()
    {
        const int OperatorCount = 128;
        var expression = $"value{string.Concat(Enumerable.Repeat(" + 1", OperatorCount))}";
        await VerifyPreferConst.VerifyAnalyzerAsync($$"""
            public class C
            {
                public int Run()
                {
                    int value = 3;
                    return {{expression}};
                }
            }
            """);
    }

    /// <summary>Verifies the nameof operator remains eligible while an ordinary same-named method is not.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NameofOperatorIsFixedAsync() =>
        VerifyPreferConst.VerifyCodeFixAsync(
            """
            public class C
            {
                public string Run()
                {
                    int {|PSH1402:value|} = 3;
                    return nameof(value);
                }
            }
            """,
            """
            public class C
            {
                public string Run()
                {
                    const int value = 3;
                    return nameof(value);
                }
            }
            """);

    /// <summary>Verifies a method spelled like nameof is checked as an ordinary overloaded call.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NameofMethodOverloadIsCleanAsync() =>
        VerifyPreferConst.VerifyAnalyzerAsync("""
            public class C
            {
                public bool Run()
                {
                    int retries = 0;
                    return @nameof(retries);
                }
                private static bool @nameof(long value) => true;
                private static bool @nameof(byte value) => false;
            }
            """);

    /// <summary>Verifies Fix All cannot jointly make numeric overloads or loop conditions constant-sensitive.</summary>
    /// <param name="body">The combined use of two otherwise constant locals.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("return Select(first, second);")]
    [Arguments("return Optional(first, second);")]
    [Arguments("while (first < second) { return true; } return false;")]
    [Arguments("while (first + int.MaxValue > second) { return true; } return false;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CombinedConstantSensitiveUsesAreCleanAsync(string body) =>
        VerifyPreferConst.VerifyAnalyzerAsync($$"""
            public class C
            {
                public bool Run()
                {
                    int first = 1;
                    int second = 5;
                    {{body}}
                }
                private static bool Select(long left, long right) => true;
                private static bool Select(byte left, byte right) => false;
                private static bool Optional(long left, long right, int extra = 0) => true;
                private static bool Optional(byte left, byte right) => false;
            }
            """);

    /// <summary>Verifies advancing counters are excluded for every loop syntax.</summary>
    /// <param name="loop">The loop advancing its counter.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("do { retries++; } while (retries < 5);")]
    [Arguments("while (retries < 5) { ++retries; }")]
    [Arguments("for (; retries < 5; retries++) { }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task AdvancingLoopCounterIsCleanAsync(string loop) =>
        VerifyPreferConst.VerifyAnalyzerAsync(
            $$"""
            public class C
            {
                public void Run()
                {
                    var retries = 0;
                    {{loop}}
                }
            }
            """);

    /// <summary>Compiles executable regression sources against shared host references.</summary>
    /// <param name="source">The source under test.</param>
    /// <returns>The library compilation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CSharpCompilation CreateCompilation(string source) =>
        CSharpCompilation.Create(
            "ConstLocalRegression",
            [CSharpSyntaxTree.ParseText(source)],
            RuntimeMetadataReferences.Platform,
            new(OutputKind.DynamicallyLinkedLibrary));
}
