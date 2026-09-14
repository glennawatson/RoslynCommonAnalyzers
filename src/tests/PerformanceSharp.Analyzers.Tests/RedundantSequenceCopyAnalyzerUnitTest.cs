// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1217RedundantSequenceCopyAnalyzer,
    PerformanceSharp.Analyzers.Psh1217RedundantSequenceCopyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1217RedundantSequenceCopyAnalyzer"/> (PSH1217 redundant sequence copies).</summary>
public class RedundantSequenceCopyAnalyzerUnitTest
{
    /// <summary>Verifies a ToCharArray copy enumerated by foreach is flagged and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForEachOverToCharArrayIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(string value)
                                  {
                                      var total = 0;
                                      foreach (var c in {|PSH1217:value.ToCharArray()|})
                                      {
                                          total += c;
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(string value)
                                       {
                                           var total = 0;
                                           foreach (var c in value)
                                           {
                                               total += c;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a Length read through a ToCharArray copy is flagged and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LengthOfToCharArrayIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(string value) => {|PSH1217:value.ToCharArray()|}.Length;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(string value) => value.Length;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an element read through a ToCharArray copy is flagged and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexerOnToCharArrayIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public char M(string value, int i) => {|PSH1217:value.ToCharArray()|}[i];
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public char M(string value, int i) => value[i];
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a copy passed where a string overload exists is flagged and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentWithStringOverloadIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(string value) => Use({|PSH1217:value.ToCharArray()|});

                                  private static void Use(char[] chars)
                                  {
                                  }

                                  private static void Use(string text)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(string value) => Use(value);

                                       private static void Use(char[] chars)
                                       {
                                       }

                                       private static void Use(string text)
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a copy passed where a span overload exists is flagged and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentWithSpanOverloadIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(string value) => Use({|PSH1217:value.ToCharArray()|});

                                  private static void Use(char[] chars)
                                  {
                                  }

                                  private static void Use(ReadOnlySpan<char> chars)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(string value) => Use(value);

                                       private static void Use(char[] chars)
                                       {
                                       }

                                       private static void Use(ReadOnlySpan<char> chars)
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a span's ToArray copy read for its length is flagged and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LengthOfSpanToArrayIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(ReadOnlySpan<char> span) => {|PSH1217:span.ToArray()|}.Length;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(ReadOnlySpan<char> span) => span.Length;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a span's ToArray copy passed to a span overload is flagged and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpanToArrayArgumentIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(ReadOnlySpan<int> span) => Use({|PSH1217:span.ToArray()|});

                                  private static void Use(int[] values)
                                  {
                                  }

                                  private static void Use(ReadOnlySpan<int> values)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(ReadOnlySpan<int> span) => Use(span);

                                       private static void Use(int[] values)
                                       {
                                       }

                                       private static void Use(ReadOnlySpan<int> values)
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a copy whose elements are written is not reported — a string cannot be mutated.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MutatedCopyIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(string value) => value.ToCharArray()[0] = 'x';
            }
            """);

    /// <summary>Verifies a copy stored in a local and then mutated is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StoredAndMutatedCopyIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public char[] M(string value)
                {
                    var chars = value.ToCharArray();
                    chars[0] = 'x';
                    return chars;
                }
            }
            """);

    /// <summary>Verifies a returned copy is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReturnedCopyIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public char[] M(string value) => value.ToCharArray();
            }
            """);

    /// <summary>Verifies a copy handed to an API that only takes an array is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArrayOnlyConsumerIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(string value) => Use(value.ToCharArray());

                private static void Use(char[] chars)
                {
                }
            }
            """);

    /// <summary>Verifies a copy is not reported when dropping it would bind a different overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The call is written for <c>Use(char[])</c>, but a <c>string</c> reaches only <c>Use(object)</c>,
    /// so dropping the copy would silently redirect the call to an overload that takes no sequence at
    /// all. The rule confirms the rewritten binding rather than assuming a sibling overload exists to
    /// catch it.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OverloadTheRewriteWouldNotReachIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(string value) => Use(value.ToCharArray());

                private static void Use(char[] chars)
                {
                }

                private static void Use(object value)
                {
                }
            }
            """);

    /// <summary>Verifies a range slice of a copy is not reported — the sliced types differ.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RangeSliceOfCopyIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public char[] M(string value) => value.ToCharArray()[1..];
            }
            """);

    /// <summary>Verifies a span copy enumerated by foreach is not reported — a span cannot cross an await.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ForEachOverSpanToArrayIsCleanAsync() =>
        VerifyAsync(
            """
            using System;

            public class C
            {
                public int M(ReadOnlySpan<char> span)
                {
                    var total = 0;
                    foreach (var c in span.ToArray())
                    {
                        total += c;
                    }

                    return total;
                }
            }
            """);

    /// <summary>Verifies a LINQ ToArray is not reported — only the span's own ToArray is a copy this rule owns.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LinqToArrayIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(IEnumerable<int> values) => values.ToArray().Length;
            }
            """);

    /// <summary>Verifies a sequence copy reached through a conditional access is not reported — rebinding the detached call would orphan its member binding.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConditionalAccessSequenceCopyIsLeftAloneAsync() =>
        VerifyAsync(
            """
            public sealed class C
            {
                public void Consume(char[] values)
                {
                }

                public void Run(C c, string text)
                {
                    c?.Consume(text.ToCharArray());
                }
            }
            """);

    /// <summary>Verifies only parameterless simple member calls have the sequence-copy shape.</summary>
    /// <param name="expression">The invocation syntax.</param>
    /// <param name="expected">Whether the invocation can be a supported copy.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value.ToCharArray()", true)]
    [Arguments("value.ToArray()", true)]
    [Arguments("value.ToCharArray(0, 1)", false)]
    [Arguments("ToCharArray()", false)]
    [Arguments("value.Other()", false)]
    [Arguments("value->ToArray()", false)]
    public async Task SequenceCopySyntaxRequiresSimpleParameterlessMemberAsync(string expression, bool expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(expression);
        await Assert.That(Psh1217RedundantSequenceCopyAnalyzer.IsSequenceCopyShape(invocation)).IsEqualTo(expected);
    }

    /// <summary>Verifies writes, unsupported consumers, and non-framework copies keep their allocation.</summary>
    /// <param name="source">The complete source containing an unsupported copy use.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { int M(string s) => s.ToCharArray(0, 1).Length; }")]
    [Arguments("class C { int M(string s) => s.ToUpper().Length; }")]
    [Arguments("class C { int M(string s) => s.ToCharArray().Rank; }")]
    [Arguments("class C { int? M(string s) => s?.ToCharArray().Length; }")]
    [Arguments("class C { int M() => Missing.ToCharArray().Length; }")]
    [Arguments("class C { static int[] ToArray() => null; int M() => C.ToArray().Length; }")]
    [Arguments("class C { int[] ToArray(int x = 0) => null; int M(C c) => c.ToArray().Length; }")]
    [Arguments("class C { char[] ToCharArray() => null; int M(C c) => c.ToCharArray().Length; }")]
    [Arguments("class C { int[] ToArray() => null; int M(C c) => c.ToArray().Length; }")]
    [Arguments("class ReadOnlySpan { public int[] ToArray() => null; } class C { int M(ReadOnlySpan s) => s.ToArray().Length; }")]
    [Arguments("namespace Other { class ReadOnlySpan<T> { public T[] ToArray() => null; } class C { int M(ReadOnlySpan<int> s) => s.ToArray().Length; } }")]
    [Arguments("namespace Other.System { class ReadOnlySpan<T> { public T[] ToArray() => null; } class C { int M(ReadOnlySpan<int> s) => s.ToArray().Length; } }")]
    [Arguments("namespace System { class ReadOnlySpan<T, U> { public T[] ToArray() => null; } } class C { int M(System.ReadOnlySpan<int, int> s) => s.ToArray().Length; }")]
    [Arguments("class C { void M(string s) { ++s.ToCharArray()[0]; } }")]
    [Arguments("class C { void M(string s) { --s.ToCharArray()[0]; } }")]
    [Arguments("class C { void M(string s) { s.ToCharArray()[0]++; } }")]
    [Arguments("class C { void M(string s) { s.ToCharArray()[0]--; } }")]
    [Arguments("class C { void M(string s) { s.ToCharArray()[0] += (char)1; } }")]
    [Arguments("class C { ref char M(string s) => ref s.ToCharArray()[0]; }")]
    [Arguments("class C { void Use(ref char c) {} void M(string s) => Use(ref s.ToCharArray()[0]); }")]
    [Arguments("class C { void Use(out char c) { c = 'a'; } void M(string s) => Use(out s.ToCharArray()[0]); }")]
    [Arguments("class C { void Use(in char c) {} void M(string s) => Use(in s.ToCharArray()[0]); }")]
    [Arguments("class C { void Use(char[] c) {} void Use(string c) {} void M(string s) => Use(c: s.ToCharArray()); }")]
    [Arguments("class C { void Use(ref char[] c) {} void M(string s) => Use(ref s.ToCharArray()); }")]
    [Arguments("class C { C(char[] c) {} C M(string s) => new C(s.ToCharArray()); }")]
    [Arguments("class C { void M(string s) => Missing(s.ToCharArray()); }")]
    [Arguments("static class Extensions { public static void Use(this string s, char[] c) {} } class C { void M(string s) => s.Use(s.ToCharArray()); }")]
    [Arguments("static class Extensions { public static void Use(this string s, char[] c) {} } class C { void M(string s) => Extensions.Use(s, s.ToCharArray()); }")]
    [Arguments("class C { void Use(in char[] c) {} void M(string s) => Use(s.ToCharArray()); }")]
    [Arguments("class C { void Use(params char[] c) {} void M(string s) => Use(s.ToCharArray()); }")]
    [Arguments("class C { void Use(params object[] c) {} void M(string s) => Use(1, s.ToCharArray()); }")]
    [Arguments("class C { void Use(object c) {} void M(string s) => Use(s.ToCharArray()); }")]
    [Arguments("class C { void Use(char[] c) {} void Use(string c, int n = 0) {} void M(string s) => Use(s.ToCharArray()); }")]
    [Arguments("class C { static void Use(char[] c) {} void Use(string c) {} void M(string s) => Use(s.ToCharArray()); }")]
    [Arguments("class C { int Use(char[] c) => 0; string Use(string c) => null; object M(string s) => Use(s.ToCharArray()); }")]
    [Arguments("class C { void Use(char[] c, int n) {} void Use(string c, long n) {} void M(string s) => Use(s.ToCharArray(), 1); }")]
    [Arguments("class B { protected void Use(char[] c) {} } class C : B { void Use(string c) {} void M(string s) => Use(s.ToCharArray()); }")]
    [Arguments("class C { int[] M(System.ReadOnlySpan<int> s) => s.ToArray()[1..]; }")]
    [Arguments("class C { void Use(object[] c) {} void M(System.ReadOnlySpan<string> s) => Use(s.ToArray()); }")]
    [Arguments("class C { void Use(string[] c) {} void Use(object c) {} void M(System.ReadOnlySpan<string> s) => Use(s.ToArray()); }")]
    public async Task UnsupportedCopyUseIsCleanAsync(string source)
    {
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1217RedundantSequenceCopyAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies read-only element uses and multi-argument overloads still remove redundant copies.</summary>
    /// <param name="source">The complete source containing one redundant copy.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { int M(System.ReadOnlySpan<int> s) => s.ToArray()[0]; }")]
    [Arguments("class C { int M(string s) => -s.ToCharArray()[0]; }")]
    [Arguments("class C { char M(string s) => s.ToCharArray()[0]!; }")]
    [Arguments("class C { void M(string s, ref char c) { c = s.ToCharArray()[0]; } }")]
    [Arguments("class C { void Use(char c) {} void M(string s) => Use(s.ToCharArray()[0]); }")]
    [Arguments("class C { void Use(int a, char[] c, int b) {} void Use(int a, string c, int b) {} void M(string s) => Use(1, s.ToCharArray(), 2); }")]
    [Arguments("class C { void Use(int[] c, int n) {} void Use(System.ReadOnlySpan<int> c, int n) {} void M(System.ReadOnlySpan<int> s) => Use(s.ToArray(), 1); }")]
    public async Task ReadOnlyCopyUseReportsAsync(string source)
    {
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1217RedundantSequenceCopyAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("PSH1217");
    }

    /// <summary>Verifies alternate span declarations cannot redirect a copy to an incompatible array or span.</summary>
    /// <param name="declarations">The alternate sequence declarations and consumer.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("""
        namespace System { class ReadOnlySpan<T> { public T[,] ToArray() => null; } }
        class C { void Use(int[,] c) {} void M(System.ReadOnlySpan<int> s) => Use(s.ToArray()); }
        """)]
    [Arguments("""
        namespace System { class ReadOnlySpan<T> { public object[] ToArray() => null; } }
        class C { void Use(object[] c) {} void M(System.ReadOnlySpan<int> s) => Use(s.ToArray()); }
        """)]
    [Arguments("""
        namespace System { class ReadOnlySpan<T> { public T[] ToArray() => null; public static implicit operator ReadOnlySpan<string>(ReadOnlySpan<T> value) => null; } }
        class C { void Use(int[] c) {} void Use(System.ReadOnlySpan<string> c) {} void M(System.ReadOnlySpan<int> s) => Use(s.ToArray()); }
        """)]
    [Arguments("""
        namespace Other { class ReadOnlySpan<T> { public static implicit operator ReadOnlySpan<T>(string value) => null; } }
        class C { void Use(char[] c) {} void Use(Other.ReadOnlySpan<char> c) {} void M(string s) => Use(s.ToCharArray()); }
        """)]
    [Arguments("""
        namespace Other.System { class ReadOnlySpan<T> { public static implicit operator ReadOnlySpan<T>(string value) => null; } }
        class C { void Use(char[] c) {} void Use(Other.System.ReadOnlySpan<char> c) {} void M(string s) => Use(s.ToCharArray()); }
        """)]
    [Arguments("""
        namespace System { class ReadOnlySpan<T> { public static implicit operator ReadOnlySpan<T>(string value) => null; } }
        class C { void Use(char[] c) {} void Use(System.ReadOnlySpan<int> c) {} void M(string s) => Use(s.ToCharArray()); }
        """)]
    public async Task IncompatibleSequenceContractIsCleanAsync(string declarations)
    {
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(declarations)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1217RedundantSequenceCopyAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
