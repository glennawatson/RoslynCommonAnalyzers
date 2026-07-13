// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1220RedundantLengthArgumentAnalyzer,
    PerformanceSharp.Analyzers.Psh1220RedundantLengthArgumentCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1220RedundantLengthArgumentAnalyzer"/> (PSH1220 redundant slice lengths).</summary>
public class RedundantLengthArgumentAnalyzerUnitTest
{
    /// <summary>Verifies a substring length that reaches the end is flagged and dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubstringToTheEndIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(string text, int start) => text.Substring(start, {|PSH1220:text.Length - start|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M(string text, int start) => text.Substring(start);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a whole-string substring starting at zero is flagged and dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubstringFromZeroForWholeLengthIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(string text) => text.Substring(0, {|PSH1220:text.Length|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M(string text) => text.Substring(0);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the same shape on AsSpan is flagged and dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsSpanToTheEndIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public ReadOnlySpan<char> M(string text, int start) => text.AsSpan(start, {|PSH1220:text.Length - start|});
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public ReadOnlySpan<char> M(string text, int start) => text.AsSpan(start);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the same shape on a span's Slice is flagged and dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpanSliceToTheEndIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public ReadOnlySpan<byte> M(ReadOnlySpan<byte> data, int start) => data.Slice(start, {|PSH1220:data.Length - start|});
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public ReadOnlySpan<byte> M(ReadOnlySpan<byte> data, int start) => data.Slice(start);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a dotted receiver path is handled, because it reads the same value both times.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldReceiverIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private readonly string _text = "abc";

                                  public string M(int start) => _text.Substring(start, {|PSH1220:_text.Length - start|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private readonly string _text = "abc";

                                       public string M(int start) => _text.Substring(start);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a length that stops short of the end is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LengthThatStopsShortIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string M(string text, int start) => text.Substring(start, text.Length - start - 1);
            }
            """);

    /// <summary>Verifies a length that only happens to reach the end at run time is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The arithmetic has to be visible in the source. A length computed somewhere else may reach the
    /// end today and not tomorrow, and the rule has no business guessing which.
    /// </remarks>
    [Test]
    public async Task OpaqueLengthIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string M(string text, int start, int length) => text.Substring(start, length);
            }
            """);

    /// <summary>Verifies a length taken from a different receiver's Length is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LengthOfAnotherStringIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string M(string text, string other, int start) => text.Substring(start, other.Length - start);
            }
            """);

    /// <summary>Verifies a receiver that does work is left alone, because the fix would delete one of its calls.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// <c>GetText().Substring(i, GetText().Length - i)</c> calls <c>GetText</c> twice. Dropping the
    /// length argument drops one of those calls, which is a change in behavior whenever the method does
    /// anything at all — so a receiver that is not a plain name path is never touched.
    /// </remarks>
    [Test]
    public async Task CallReceiverIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                private static string GetText() => "abc";

                public string M(int start) => GetText().Substring(start, GetText().Length - start);
            }
            """);

    /// <summary>Verifies a start argument that does work is left alone for the same reason.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallStartArgumentIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                private static int Next() => 1;

                public string M(string text) => text.Substring(Next(), text.Length - Next());
            }
            """);

    /// <summary>Verifies a user-defined two-argument Slice is not shortened, whatever its arithmetic looks like.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Somebody else's <c>Slice(int)</c> is defined to do whatever they wrote, which need not be "run
    /// to the end". Only the framework slices whose one-argument form has that meaning are shortened.
    /// </remarks>
    [Test]
    public async Task UserDefinedSliceIsCleanAsync()
        => await VerifyAsync(
            """
            public class Buffer
            {
                public int Length => 8;

                public Buffer Slice(int start) => this;

                public Buffer Slice(int start, int length) => this;
            }

            public class C
            {
                public Buffer M(Buffer buffer, int start) => buffer.Slice(start, buffer.Length - start);
            }
            """);

    /// <summary>
    /// Verifies the string form still reports against netstandard2.0, because the overload it suggests
    /// exists there.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// <c>string.Substring(int)</c> is as old as <see cref="string"/> itself, so the suggestion is
    /// actionable on every target and staying silent would be a false negative. What the rule does
    /// prove on this target is the other half of the contract: the shortened call is bound before the
    /// diagnostic is raised, so it is known to compile here.
    /// </remarks>
    [Test]
    public async Task NetStandard20StillReportsTheStringFormAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(string text, int start) => text.Substring(start, {|PSH1220:text.Length - start|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M(string text, int start) => text.Substring(start);
                                   }
                                   """;
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source,
            FixedCode = FixedSource,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// Verifies the span forms are silent against netstandard2.0, where <c>AsSpan</c> does not exist.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The span slices cannot even be written on that target, and the semantic gate would refuse them
    /// anyway: the rule only shortens a slice whose containing type it recognizes, and the shortened
    /// call is bound before anything is reported.
    /// </remarks>
    [Test]
    public async Task NetStandard20IsSilentOnTheSpanFormAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = """
                       public class C
                       {
                           public char[] M(char[] data, int start)
                           {
                               var copy = new char[data.Length - start];
                               System.Array.Copy(data, start, copy, 0, copy.Length);
                               return copy;
                           }
                       }
                       """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
