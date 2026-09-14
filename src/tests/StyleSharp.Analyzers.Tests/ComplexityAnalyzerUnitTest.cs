// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using VerifyComplexity = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.FunctionComplexityAnalyzer>;
using VerifySingleIterationLoop = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.SingleIterationLoopAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for function-complexity maintainability rules.</summary>
public class ComplexityAnalyzerUnitTest
{
    /// <summary>The configuration path used by the complexity tests.</summary>
    private const string EditorConfigPath = "/.editorconfig";

    /// <summary>Verifies temporarily bodyless declarations encountered during editing are ignored.</summary>
    /// <param name="source">The incomplete declaration syntax.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { C(); }")]
    [Arguments("class C { ~C(); }")]
    [Arguments("class C { static C operator +(C a, C b); }")]
    [Arguments("class C { static implicit operator int(C a); }")]
    [Arguments("class C { void M() { void Local(); } }")]
    public async Task IncompleteDeclarationsAreIgnoredAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            nameof(IncompleteDeclarationsAreIgnoredAsync),
            [CSharpSyntaxTree.ParseText(source)],
            RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new FunctionComplexityAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies declarations without executable bodies do not consume complexity budgets.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task BodylessDeclarationsAreIgnoredAsync() =>
        VerifyComplexity.VerifyAnalyzerAsync("abstract class C { public abstract int M(); public abstract int P { get; set; } }");

    /// <summary>Verifies each control-flow form contributes its specified branching and nesting cost.</summary>
    /// <param name="body">The method body to measure.</param>
    /// <param name="branching">The expected cyclomatic complexity.</param>
    /// <param name="cognitive">The expected cognitive complexity.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("if (b) { } else if (!b) { } else { }", 3, 3)]
    [Arguments("for (int i = 0; i < 2; i++) { if (b) { } }", 3, 3)]
    [Arguments("foreach (var i in new int[0]) { if (b) { } }", 3, 3)]
    [Arguments("while (b) { if (b) break; }", 3, 3)]
    [Arguments("do { if (b) break; } while (b);", 3, 3)]
    [Arguments("try { M(b); } catch (System.Exception) { if (b) { } }", 2, 4)]
    [Arguments("if (b) goto end; end: return;", 2, 3)]
    [Arguments("_ = ((b && b) && b) || b;", 4, 2)]
    [Arguments("_ = 1 is ((> 0 and < 3) and < 4) or 5;", 4, 2)]
    [Arguments("string s = null; s ??= \"a\"; _ = s ?? \"b\"; _ = s?.Length;", 4, 0)]
    [Arguments("M(b); M(); this.M(b);", 1, 1)]
    [Arguments("System.Action<bool> a = x => { if (x) { if (x) { } } }; System.Action c = () => { if (b) { } }; System.Action d = delegate { if (b) { } };", 1, 0)]
    public async Task ControlFlowCostsAreStableAsync(string body, int branching, int cognitive)
    {
        var source = $$"""class C { void M() { } {|#0:void M(bool b) { {{body}} }|} }""";
        var test = new VerifyComplexity.Test { TestCode = source };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, "root = true\n[*.cs]\nstylesharp.max_cyclomatic_complexity = 1\nstylesharp.max_cognitive_complexity = 1\n"));
        if (branching > 1)
        {
            test.ExpectedDiagnostics.Add(VerifyComplexity.Diagnostic("SST1442").WithLocation(0).WithArguments(1, branching, "method"));
        }

        if (cognitive > 1)
        {
            test.ExpectedDiagnostics.Add(VerifyComplexity.Diagnostic("SST1443").WithLocation(0).WithArguments("method", cognitive, 1));
        }

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies executable declarations use their own reporting name and threshold.</summary>
    /// <param name="declaration">The declaration containing nested decisions.</param>
    /// <param name="kind">The declaration name in the diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public C(bool b) { if (b) { if (b) { } } }", "constructor")]
    [Arguments("public C() => _ = true ? (false ? 1 : 2) : 3;", "constructor")]
    [Arguments("~C() { if (true) { if (true) { } } }", "destructor")]
    [Arguments("~C() => _ = true ? (false ? 1 : 2) : 3;", "destructor")]
    [Arguments("public static C operator +(C a, C b) { if (true) { if (true) { } } return a; }", "operator")]
    [Arguments("public static C operator +(C a, C b) => true ? (false ? a : b) : a;", "operator")]
    [Arguments("public static implicit operator int(C a) { if (true) { if (true) { } } return 0; }", "operator")]
    [Arguments("public static implicit operator int(C a) => true ? (false ? 1 : 2) : 3;", "operator")]
    [Arguments("public int P => true ? (false ? 1 : 2) : 3;", "property")]
    [Arguments("public int M() => true ? (false ? 1 : 2) : 3;", "method")]
    public async Task DeclarationKindsUseConfiguredThresholdsAsync(string declaration, string kind)
    {
        const int Maximum = 2;
        const int Complexity = 3;
        var test = new VerifyComplexity.Test { TestCode = $$"""class C { {|#0:{{declaration}}|} }""" };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.max_cyclomatic_complexity = 2
            stylesharp.max_cognitive_complexity = 2
            stylesharp.max_property_cognitive_complexity = 2
            """));
        test.ExpectedDiagnostics.Add(VerifyComplexity.Diagnostic("SST1442").WithLocation(0).WithArguments(Maximum, Complexity, kind));
        test.ExpectedDiagnostics.Add(VerifyComplexity.Diagnostic("SST1443").WithLocation(0).WithArguments(kind, Complexity, Maximum));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies rule options take precedence, invalid values fall back, and equality is allowed.</summary>
    /// <param name="ruleKey">The rule-specific option.</param>
    /// <param name="ruleValue">The rule-specific setting.</param>
    /// <param name="generalValue">The fallback setting.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task ThresholdPrecedenceAndFallbackAsync(
        [Matrix("SST1442.max_cyclomatic_complexity", "SST1443.max_cognitive_complexity", "SST1443.max_property_cognitive_complexity")] string ruleKey,
        [Matrix("1", "3", "invalid", "0", "-1")] string ruleValue,
        [Matrix("1", "3", "invalid", "0", "-1")] string generalValue)
    {
        var generalKey = ruleKey[(ruleKey.IndexOf('.', StringComparison.Ordinal) + 1)..];
        var diagnosticId = ruleKey[..ruleKey.IndexOf('.', StringComparison.Ordinal)];
        var declaration = ruleKey.Contains("property", StringComparison.Ordinal)
            ? "int P => true ? (false ? 1 : 2) : 3;"
            : "int M() => true ? (false ? 1 : 2) : 3;";
        var reports = ruleValue == "1" || (ruleValue != "3" && generalValue == "1");
        var marked = reports ? $$"""{|{{diagnosticId}}:{{declaration}}|}""" : declaration;
        var test = new VerifyComplexity.Test { TestCode = $$"""class C { {{marked}} }""" };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, $"root = true\n[*.cs]\nstylesharp.{ruleKey} = {ruleValue}\nstylesharp.{generalKey} = {generalValue}\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies accessors and local functions are measured separately from their enclosing declarations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AccessorsAndLocalFunctionsUseIndependentBudgetsAsync()
    {
        var test = new VerifyComplexity.Test
        {
            TestCode = """
                       class C
                       {
                           public int P
                           {
                               {|SST1443:get { if (true) { if (true) { } } return 0; }|}
                               {|SST1443:set => _ = true ? (false ? 1 : 2) : 3;|}
                           }
                           public int Q { get; set; }
                           public void M()
                           {
                               {|SST1443:int Local() => true ? (false ? 1 : 2) : 3;|}
                               {|SST1443:int Other() { if (true) { if (true) { } } return 0; }|}
                           }
                       }
                       """,
        };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, "root = true\n[*.cs]\nstylesharp.max_cognitive_complexity = 2\nstylesharp.max_property_cognitive_complexity = 2\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies SST1442 reports methods over the default branching-complexity threshold.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CyclomaticComplexityOverDefaultThresholdIsReportedAsync() =>
        VerifyComplexity.VerifyAnalyzerAsync(
            """
            public class C
            {
                {|SST1442:public int M(int value)
                {
                    var result = 0;
                    if (value == 0) result++;
                    if (value == 1) result++;
                    if (value == 2) result++;
                    if (value == 3) result++;
                    if (value == 4) result++;
                    if (value == 5) result++;
                    if (value == 6) result++;
                    if (value == 7) result++;
                    if (value == 8) result++;
                    if (value == 9) result++;
                    return result;
                }|}
            }
            """);

    /// <summary>Verifies the SST1442 threshold can be raised per tree.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CyclomaticComplexityThresholdIsConfigurableAsync()
    {
        var test = new VerifyComplexity.Test
        {
            TestCode = """
                       public class C
                       {
                           public int M(int value)
                           {
                               var result = 0;
                               if (value == 0) result++;
                               if (value == 1) result++;
                               if (value == 2) result++;
                               if (value == 3) result++;
                               if (value == 4) result++;
                               if (value == 5) result++;
                               if (value == 6) result++;
                               if (value == 7) result++;
                               if (value == 8) result++;
                               if (value == 9) result++;
                               return result;
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, """
            root = true
            [*.cs]
            stylesharp.SST1442.max_cyclomatic_complexity = 11

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies wide switch expressions do not inflate complexity.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WideSwitchExpressionIsCleanAsync() =>
        VerifyComplexity.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int value) =>
                    value switch
                    {
                        0 => 0,
                        1 => 1,
                        2 => 2,
                        3 => 3,
                        4 => 4,
                        5 => 5,
                        6 => 6,
                        7 => 7,
                        8 => 8,
                        9 => 9,
                        10 => 10,
                        11 => 11,
                        _ => -1
                    };
            }
            """);

    /// <summary>Verifies wide switch statements do not inflate complexity.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WideSwitchStatementIsCleanAsync() =>
        VerifyComplexity.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int value)
                {
                    switch (value)
                    {
                        case 0: return 0;
                        case 1: return 1;
                        case 2: return 2;
                        case 3: return 3;
                        case 4: return 4;
                        case 5: return 5;
                        case 6: return 6;
                        case 7: return 7;
                        case 8: return 8;
                        case 9: return 9;
                        case 10: return 10;
                        case 11: return 11;
                        default: return -1;
                    }
                }
            }
            """);

    /// <summary>Verifies SST1443 reports methods over the default nested-flow threshold.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CognitiveComplexityOverDefaultThresholdIsReportedAsync() =>
        VerifyComplexity.VerifyAnalyzerAsync(
            """
            public class C
            {
                {|SST1443:public int M(int value)
                {
                    var result = 0;
                    if (value > 0)
                    {
                        if (value > 1)
                        {
                            if (value > 2)
                            {
                                if (value > 3)
                                {
                                    if (value > 4)
                                    {
                                        if (value > 5)
                                        {
                                            result = value;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    return result;
                }|}
            }
            """);

    /// <summary>Verifies SST1444 reports an unconditional terminating jump in a loop.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleIterationLoopWithUnconditionalReturnIsReportedAsync() =>
        VerifySingleIterationLoop.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int[] values)
                {
                    {|SST1444:foreach (var value in values)
                    {
                        return value;
                    }|}

                    return 0;
                }
            }
            """);

    /// <summary>Verifies each terminating jump and loop syntax is recognized.</summary>
    /// <param name="loop">The single-iteration loop.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("for (;;) { break; }")]
    [Arguments("while (true) { continue; }")]
    [Arguments("do { throw new System.Exception(); } while (true);")]
    [Arguments("foreach (int value in new int[0]) { return; }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnconditionalLoopJumpsAreReportedAsync(string loop) =>
        VerifySingleIterationLoop.VerifyAnalyzerAsync($$"""class C { void M() { {|SST1444:{{loop}}|} } }""");

    /// <summary>Verifies conditional jumps and nested function jumps do not terminate the enclosing loop.</summary>
    /// <param name="body">The loop body.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("if (flag) return;")]
    [Arguments("if (flag) { } else return;")]
    [Arguments("if (flag) throw new System.Exception();")]
    [Arguments("switch (value) { case 0: break; default: return; }")]
    [Arguments("try { if (flag) throw new System.Exception(); } catch { return; }")]
    [Arguments("try { continue; } finally { }")]
    [Arguments("System.Action action = () => { return; };")]
    [Arguments("System.Action<int> action = x => { throw new System.Exception(); };")]
    [Arguments("System.Action action = delegate { return; };")]
    [Arguments("void Local() { return; }")]
    [Arguments("for (; flag;) { if (flag) break; }")]
    [Arguments("do { if (flag) break; } while (flag);")]
    [Arguments("foreach (var item in new int[0]) { if (flag) break; }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConditionalAndNestedJumpsAreIgnoredAsync(string body) =>
        VerifySingleIterationLoop.VerifyAnalyzerAsync($$"""class C { void M(bool flag, int value) { while (flag) { {{body}} } } }""");

    /// <summary>Verifies SST1444 ignores conditional continues and does not report the outer loop for nested-loop jumps.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConditionalContinueAndNestedLoopJumpsDoNotReportOuterLoopAsync() =>
        VerifySingleIterationLoop.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int M(int[] values)
                {
                    foreach (var value in values)
                    {
                        if (value < 0)
                        {
                            continue;
                        }

                        {|SST1444:while (value > 0)
                        {
                            break;
                        }|}

                        return value;
                    }

                    return 0;
                }
            }
            """);
}
