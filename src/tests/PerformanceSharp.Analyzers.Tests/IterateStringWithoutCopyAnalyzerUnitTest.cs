// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1226IterateStringWithoutCopyAnalyzer,
    PerformanceSharp.Analyzers.Psh1226IterateStringWithoutCopyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1226IterateStringWithoutCopyAnalyzer"/> (PSH1226 iterate a string without copying it).</summary>
public class IterateStringWithoutCopyAnalyzerUnitTest
{
    /// <summary>Verifies a char[] local only enumerated by foreach is reported and retyped to the string.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForEachedCharArrayLocalIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(string text)
                                  {
                                      char[] chars = {|PSH1226:text.ToCharArray()|};
                                      var count = 0;
                                      foreach (var c in chars)
                                      {
                                          if (c == 'a')
                                          {
                                              count++;
                                          }
                                      }

                                      return count;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(string text)
                                       {
                                           string chars = text;
                                           var count = 0;
                                           foreach (var c in chars)
                                           {
                                               if (c == 'a')
                                               {
                                                   count++;
                                               }
                                           }

                                           return count;
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a var local read by Length and indexer in a for loop is reported and re-inferred as the string.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexedVarLocalIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(string text)
                                  {
                                      var chars = {|PSH1226:text.ToCharArray()|};
                                      var sum = 0;
                                      for (int i = 0; i < chars.Length; i++)
                                      {
                                          sum += chars[i];
                                      }

                                      return sum;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(string text)
                                       {
                                           var chars = text;
                                           var sum = 0;
                                           for (int i = 0; i < chars.Length; i++)
                                           {
                                               sum += chars[i];
                                           }

                                           return sum;
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a copy handed straight to a foreach, with no local, is not this rule's shape.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DirectForEachWithoutLocalIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public int M(string text)
                {
                    var count = 0;
                    foreach (var c in text.ToCharArray())
                    {
                        count++;
                    }

                    return count;
                }
            }
            """);

    /// <summary>Verifies a mutated array is left alone: a string cannot stand in for a writable buffer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MutatedLocalIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public char[] M(string text)
                {
                    var chars = text.ToCharArray();
                    chars[0] = 'x';
                    return chars;
                }
            }
            """);

    /// <summary>Verifies an array passed as an argument is left alone: the callee may want a real array.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PassedLocalIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public void M(string text)
                {
                    var chars = text.ToCharArray();
                    Consume(chars);
                }

                private static void Consume(char[] value)
                {
                }
            }
            """);

    /// <summary>Verifies an array whose member other than Length is read is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClonedLocalIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public object M(string text)
                {
                    var chars = text.ToCharArray();
                    return chars.Clone();
                }
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source) => await VerifyAsync(source, source);
}
