// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyIndentation = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1137ElementIndentationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the element-indentation rule (SST1137).</summary>
public class ElementIndentationAnalyzerUnitTest
{
    /// <summary>Verifies enum siblings establish and retain one reference column.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EnumMembersUseTheFirstOwnLineIndentAsync() =>
        VerifyIndentation.VerifyAnalyzerAsync(
            """
            enum E { Inline,
                First,
                  {|SST1137:Second|},
                Third,
            }
            enum Empty { }
            """);

    /// <summary>Verifies namespace siblings are compared without counting declarations sharing a line.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamespaceMembersUseTheFirstOwnLineIndentAsync() =>
        VerifyIndentation.VerifyAnalyzerAsync(
            """
            namespace N { class Inline { }
                class First { }
                  {|SST1137:class|} Second { }
                class Third { }
            }
            namespace Empty { }
            """);

    /// <summary>Verifies every supported type container compares its own members.</summary>
    /// <param name="kind">The type declaration keyword.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class")]
    [Arguments("struct")]
    [Arguments("record")]
    [Arguments("record struct")]
    [Arguments("interface")]
    public Task TypeMembersUseTheFirstOwnLineIndentAsync(string kind) =>
        VerifyIndentation.VerifyAnalyzerAsync(
            $$"""
            {{kind}} C
            {
                int First { get; }
                  {|SST1137:int|} Second { get; }
                int Third { get; }
            }
            """);

    /// <summary>Verifies same-line elements do not establish a reference indentation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SharedLinesAreIgnoredAndZeroIndentIsRetainedAsync() =>
        VerifyIndentation.VerifyAnalyzerAsync(
            """
            class C { int First; int Second;
            int Third;
              {|SST1137:int|} Fourth;
            void M() { int first = 0; int second = 0;
            int third = 0;
              {|SST1137:int|} fourth = 0;
            }
            }
            enum E { First, Second }
            namespace N { class First { } class Second { } }
            """);

    /// <summary>Verifies a statement indented differently from its siblings is reported (SST1137).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MisalignedStatementReportedAsync() =>
        VerifyIndentation.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static void M()
                {
                    var a = 1;
                      {|SST1137:var|} b = 2;
                    var c = 3;
                }
            }
            """);

    /// <summary>Verifies a member indented differently from its siblings is reported (SST1137).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MisalignedMemberReportedAsync() =>
        VerifyIndentation.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int a;
                    {|SST1137:private|} int b;
                private int c;
            }
            """);

    /// <summary>Verifies consistently indented siblings are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConsistentIndentationIsCleanAsync() =>
        VerifyIndentation.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int a;
                private int b;

                private void M()
                {
                    var x = 1;
                    var y = 2;
                }
            }
            """);
}
