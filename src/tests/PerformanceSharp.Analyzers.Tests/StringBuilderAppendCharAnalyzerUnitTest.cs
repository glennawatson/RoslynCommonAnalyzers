// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyAppendChar = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1202StringBuilderAppendCharAnalyzer,
    PerformanceSharp.Analyzers.Psh1202StringBuilderAppendCharCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1202 (StringBuilder should append single characters as char) and its code fix.</summary>
public class StringBuilderAppendCharAnalyzerUnitTest
{
    /// <summary>Verifies Append with a single-character literal is reported (PSH1202) and fixed to the char overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendSingleCharacterLiteralReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.Append({|PSH1202:"x"|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder)
                                           => builder.Append('x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Insert with a single-character literal is reported and fixed to the char overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InsertSingleCharacterLiteralReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.Insert(0, {|PSH1202:"x"|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder)
                                           => builder.Insert(0, 'x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a multi-character literal is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendMultiCharacterLiteralIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.Append("xy");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a non-literal string argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendVariableIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder, string value)
                                      => builder.Append(value);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies AppendLine is not reported — there is no char AppendLine overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendLineIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.AppendLine("x");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies chained Append calls are each reported and all fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ChainedAppendsAllReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.Append({|PSH1202:"a"|}).Append({|PSH1202:"b"|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder)
                                           => builder.Append('a').Append('b');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a double-quote literal is fixed with correct char escaping.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AppendDoubleQuoteLiteralEscapedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void M(System.Text.StringBuilder builder)
                                      => builder.Append({|PSH1202:"\""|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void M(System.Text.StringBuilder builder)
                                           => builder.Append('"');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyAppendChar.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
