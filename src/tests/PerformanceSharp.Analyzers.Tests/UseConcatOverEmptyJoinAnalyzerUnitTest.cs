// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyEmptyJoin = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1215UseConcatOverEmptyJoinAnalyzer,
    PerformanceSharp.Analyzers.Psh1215UseConcatOverEmptyJoinCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1215 (concatenate when there is no separator) and its code fix.</summary>
public class UseConcatOverEmptyJoinAnalyzerUnitTest
{
    /// <summary>Verifies <c>string.Join</c> with an empty literal separator over a string array is reported (PSH1215) and rewritten to <c>string.Concat</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyLiteralSeparatorWithArrayReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(string[] parts)
                                      => {|PSH1215:string.Join("", parts)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M(string[] parts)
                                           => string.Concat(parts);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies <c>string.Join</c> with a <c>string.Empty</c> separator over an <c>IEnumerable&lt;string&gt;</c> is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringEmptySeparatorWithEnumerableReplacedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public string M(IEnumerable<string> parts)
                                      => {|PSH1215:string.Join(string.Empty, parts)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public string M(IEnumerable<string> parts)
                                           => string.Concat(parts);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the expanded params call form keeps every value argument: <c>Join("", a, b, c)</c> becomes <c>Concat(a, b, c)</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpandedParamsFormReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(string a, string b, string c)
                                      => {|PSH1215:string.Join("", a, b, c)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M(string a, string b, string c)
                                           => string.Concat(a, b, c);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the generic <c>Join&lt;T&gt;</c> shape over an <c>IEnumerable&lt;int&gt;</c> is reported and rewritten to the generic Concat.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericEnumerableReplacedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public string M(IEnumerable<int> numbers)
                                      => {|PSH1215:string.Join("", numbers)|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public string M(IEnumerable<int> numbers)
                                           => string.Concat(numbers);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a non-empty separator is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonEmptySeparatorIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public string M(string[] parts)
                    => string.Join(",", parts);
            }
            """);

    /// <summary>Verifies the start/count overload <c>Join(string, string[], int, int)</c> is not reported — it has no Concat equivalent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StartCountOverloadIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public string M(string[] parts)
                    => string.Join("", parts, 0, 2);
            }
            """);

    /// <summary>Verifies a char separator is not reported — it can never be the empty string.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CharSeparatorIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public class C
            {
                public string M(string[] parts)
                    => string.Join(' ', parts);
            }
            """);

    /// <summary>Verifies a user-defined <c>String.Join</c> method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedJoinIsCleanAsync()
        => await VerifyNet90CleanAsync(
            """
            public static class String
            {
                public static string Join(string separator, params string[] values) => separator;
            }

            public class C
            {
                public string M(string[] parts)
                    => String.Join("", parts);
            }
            """);

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyEmptyJoin.Test
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
    private static async Task VerifyNet90CleanAsync(string source)
        => await VerifyNet90Async(source, source);
}
