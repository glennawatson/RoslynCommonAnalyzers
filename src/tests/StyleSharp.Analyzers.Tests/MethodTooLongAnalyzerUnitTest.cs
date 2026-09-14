// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using VerifyMemberLength = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1523MethodTooLongAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1523 (members should not be too long).</summary>
public class MethodTooLongAnalyzerUnitTest
{
    /// <summary>The path the analyzer config file is added at in the test workspace.</summary>
    private const string EditorConfigPath = "/.editorconfig";

    /// <summary>The one-line limit used to measure minimal multiline bodies.</summary>
    private const string OneLineMaximum = "stylesharp.SST1523.max_member_lines = 1";

    /// <summary>A type carrying one over-length instance of every member kind the rule measures.</summary>
    private const string EveryMemberKindSource = """
        public class C
        {
            private int _value;

            public {|SST1523:C|}()
            {
                _value = 1;
                _value = 2;
                _value = 3;
            }

            public int Value
            {
                {|SST1523:get|}
                {
                    var local = _value;
                    local += 1;
                    return local;
                }

                {|SST1523:set|}
                {
                    _value = value;
                    _value += 1;
                    _value += 2;
                }
            }

            public static C {|SST1523:operator|} +(C left, C right)
            {
                var result = new C();
                result._value = left._value;
                return result;
            }

            public static explicit {|SST1523:operator|} int(C value)
            {
                var result = value._value;
                result += 1;
                return result;
            }

            public void {|SST1523:Host|}()
            {
                int {|SST1523:Inner|}()
                {
                    var local = 1;
                    local += 2;
                    return local;
                }

                _value = Inner();
            }
        }
        """;

    /// <summary>Verifies a method over the default maximum of 60 code lines is reported and a shorter one is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodOverTheDefaultMaximumIsReportedAsync()
    {
        const int DefaultMaxMemberLines = 60;
        const int LongMethodStatementCount = 60;
        const int ShortMethodStatementCount = 20;
        const int LongMethodMeasuredLineCount = 65;
        const int ReportedMethodNameLineNumber = 3;
        const int ReportedMethodNameStartColumn = 16;
        const int ReportedMethodNameEndColumn = 20;

        var test = new VerifyMemberLength.Test
        {
            TestCode = $$"""
                       public class C
                       {
                           public int Long()
                           {
                               var total = 0;
                       {{BuildStatements(LongMethodStatementCount)}}
                               return total;
                           }

                           public int Short()
                           {
                               var total = 0;
                       {{BuildStatements(ShortMethodStatementCount)}}
                               return total;
                           }
                       }
                       """,
        };

        // Signature, both braces, the declaration, 60 additions and the return.
        test.ExpectedDiagnostics.Add(
            VerifyMemberLength.Diagnostic()
                .WithSpan(ReportedMethodNameLineNumber, ReportedMethodNameStartColumn, ReportedMethodNameLineNumber, ReportedMethodNameEndColumn)
                .WithArguments("Long", LongMethodMeasuredLineCount, DefaultMaxMemberLines));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies blank lines and comments inside a member do not count toward its length.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankLinesAndCommentsDoNotCountAsync()
    {
        var test = new VerifyMemberLength.Test
        {
            TestCode = """
                       public class C
                       {
                           public int Explained()
                           {
                               // The first step.
                               var total = 1;

                               // The second step.
                               total += 2;

                               // And back it comes.
                               return total;
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, BuildConfig("stylesharp.SST1523.max_member_lines = 7")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies every measured member kind is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryMemberKindIsMeasuredAsync()
    {
        var test = new VerifyMemberLength.Test { TestCode = EveryMemberKindSource };

        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, BuildConfig("stylesharp.SST1523.max_member_lines = 4")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a declaration with no body has nothing to split and is never measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BodilessDeclarationsAreCleanAsync()
    {
        var test = new VerifyMemberLength.Test
        {
            TestCode = """
                       public interface IContract
                       {
                           int Compute(int a, int b, int c);
                       }

                       public abstract class Base
                       {
                           public int Auto { get; set; }

                           public abstract int Compute(int a, int b, int c);
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, BuildConfig(OneLineMaximum)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule-specific maximum overrides the project-wide one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleSpecificMaximumWinsOverGeneralAsync()
    {
        var test = new VerifyMemberLength.Test
        {
            TestCode = """
                       public class C
                       {
                           public int {|SST1523:Five|}()
                           {
                               var total = 1;
                               total += 2;
                               return total;
                           }

                           public int Three() => 1;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            (EditorConfigPath, BuildConfig("stylesharp.max_member_lines = 90", "stylesharp.SST1523.max_member_lines = 4")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide maximum applies when no rule-specific key is set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralMaximumAppliesAsync()
    {
        var test = new VerifyMemberLength.Test
        {
            TestCode = """
                       public class C
                       {
                           public int {|SST1523:Five|}()
                           {
                               var total = 1;
                               total += 2;
                               return total;
                           }
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, BuildConfig("stylesharp.max_member_lines = 4")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies indexer, event and init accessor diagnostics include the owning member's name.</summary>
    /// <param name="member">A member with a two-line accessor.</param>
    /// <param name="name">The accessor name in the diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public int this[int index] { {|#0:get|} =>\n 1; }", "this[].get")]
    [Arguments("public event System.Action Changed { {|#0:add|} =>\n System.GC.KeepAlive(value); remove { } }", "Changed.add")]
    [Arguments("public event System.Action Changed { add { } {|#0:remove|} =>\n System.GC.KeepAlive(value); }", "Changed.remove")]
    [Arguments("public int Value { get => 1; {|#0:init|} =>\n System.GC.KeepAlive(value); }", "Value.init")]
    public async Task AccessorNamesIncludeTheirOwnerAsync(string member, string name)
    {
        const int AccessorLines = 2;
        var test = new VerifyMemberLength.Test { TestCode = $"class C {{ {member} }} namespace System.Runtime.CompilerServices {{ internal static class IsExternalInit {{ }} }}" };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, BuildConfig(OneLineMaximum)));
        test.ExpectedDiagnostics.Add(VerifyMemberLength.Diagnostic().WithLocation(0).WithArguments(name, AccessorLines, 1));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies expression-bodied local functions are measured independently from their containing method.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExpressionBodiedLocalFunctionIsMeasuredAsync()
    {
        var test = new VerifyMemberLength.Test { TestCode = "class C { int {|SST1523:Host|}() { int {|SST1523:Local|}() =>\n 1; return Local(); } }" };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, BuildConfig(OneLineMaximum)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies invalid maxima fall back to the default rather than reporting short members.</summary>
    /// <param name="maximum">An invalid maximum value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("0")]
    [Arguments("-1")]
    [Arguments("invalid")]
    public async Task InvalidMaximumUsesDefaultAsync(string maximum)
    {
        var test = new VerifyMemberLength.Test { TestCode = "class C { int M() =>\n 1; }" };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, BuildConfig($"stylesharp.SST1523.max_member_lines = {maximum}", $"stylesharp.max_member_lines = {maximum}")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an invalid rule-specific maximum still permits a valid general maximum.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InvalidRuleMaximumFallsBackToGeneralAsync()
    {
        var test = new VerifyMemberLength.Test { TestCode = "class C { int {|SST1523:M|}() =>\n 1; }" };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, BuildConfig("stylesharp.SST1523.max_member_lines = invalid", "stylesharp.max_member_lines = 1")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the configured maximum is inclusive for an expression-bodied method.</summary>
    /// <param name="maximum">The configured line limit.</param>
    /// <param name="reports">Whether the two-line method exceeds it.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(1, true)]
    [Arguments(2, false)]
    public async Task MemberAtMaximumIsCleanAsync(int maximum, bool reports)
    {
        var test = new VerifyMemberLength.Test { TestCode = $"class C {{ int {(reports ? "{|SST1523:M|}" : "M")}() =>\n 1; }}" };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, BuildConfig($"stylesharp.SST1523.max_member_lines = {maximum}")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an incomplete local function declaration has no body to measure.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BodilessLocalFunctionIsCleanAsync()
    {
        var test = new VerifyMemberLength.Test { TestCode = "class C { void Host() { void Local(); } }", CompilerDiagnostics = CompilerDiagnostics.None };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Builds the requested number of accumulator statements.</summary>
    /// <param name="count">The number of statements to emit.</param>
    /// <returns>The generated statements, one per line.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string BuildStatements(int count) =>
        string.Join("\n", Enumerable.Range(0, count).Select(static i => $"        total += {i};"));

    /// <summary>Builds an editor config file body from the supplied keys.</summary>
    /// <param name="entries">The keys to write under the C# section.</param>
    /// <returns>The editor config text.</returns>
    private static string BuildConfig(params string[] entries) =>
        $"root = true\n[*.cs]\n{string.Join("\n", entries)}\n";
}
