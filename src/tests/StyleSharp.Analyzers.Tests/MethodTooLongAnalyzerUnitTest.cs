// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyMemberLength = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1523MethodTooLongAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1523 (members should not be too long).</summary>
public class MethodTooLongAnalyzerUnitTest
{
    /// <summary>Verifies a method over the default maximum of 60 code lines is reported and a shorter one is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodOverTheDefaultMaximumIsReportedAsync()
    {
        var test = new VerifyMemberLength.Test
        {
            TestCode = $$"""
                       public class C
                       {
                           public int Long()
                           {
                               var total = 0;
                       {{BuildStatements(60)}}
                               return total;
                           }

                           public int Short()
                           {
                               var total = 0;
                       {{BuildStatements(20)}}
                               return total;
                           }
                       }
                       """,
        };

        // Signature, both braces, the declaration, 60 additions and the return.
        test.ExpectedDiagnostics.Add(VerifyMemberLength.Diagnostic().WithSpan(3, 16, 3, 20).WithArguments("Long", 65, 60));
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

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.SST1523.max_member_lines = 7")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies every measured member kind is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryMemberKindIsMeasuredAsync()
    {
        var test = new VerifyMemberLength.Test
        {
            TestCode = """
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
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.SST1523.max_member_lines = 4")));
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

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.SST1523.max_member_lines = 1")));
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
            ("/.editorconfig", BuildConfig("stylesharp.max_member_lines = 90", "stylesharp.SST1523.max_member_lines = 4")));
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

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", BuildConfig("stylesharp.max_member_lines = 4")));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Builds the requested number of accumulator statements.</summary>
    /// <param name="count">The number of statements to emit.</param>
    /// <returns>The generated statements, one per line.</returns>
    private static string BuildStatements(int count)
        => string.Join("\n", Enumerable.Range(0, count).Select(static i => $"        total += {i};"));

    /// <summary>Builds an editor config file body from the supplied keys.</summary>
    /// <param name="entries">The keys to write under the C# section.</param>
    /// <returns>The editor config text.</returns>
    private static string BuildConfig(params string[] entries)
        => "root = true\n[*.cs]\n" + string.Join("\n", entries) + "\n";
}
