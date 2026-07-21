// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

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
    [Test]
    public async Task MutatedCopyIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M(string value) => value.ToCharArray()[0] = 'x';
            }
            """);

    /// <summary>Verifies a copy stored in a local and then mutated is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StoredAndMutatedCopyIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ReturnedCopyIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public char[] M(string value) => value.ToCharArray();
            }
            """);

    /// <summary>Verifies a copy handed to an API that only takes an array is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayOnlyConsumerIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task OverloadTheRewriteWouldNotReachIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task RangeSliceOfCopyIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public char[] M(string value) => value.ToCharArray()[1..];
            }
            """);

    /// <summary>Verifies a span copy enumerated by foreach is not reported — a span cannot cross an await.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForEachOverSpanToArrayIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task LinqToArrayIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ConditionalAccessSequenceCopyIsLeftAloneAsync()
        => await VerifyAsync(
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
