// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1113UseNaturalOrderAnalyzer,
    PerformanceSharp.Analyzers.Psh1113UseNaturalOrderCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1113UseNaturalOrderAnalyzer"/> (PSH1113 natural ordering).</summary>
public class UseNaturalOrderAnalyzerUnitTest
{
    /// <summary>Verifies an identity ascending sort is flagged and rewritten to Order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdentityOrderByIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public IEnumerable<int> M(int[] values) => values.{|PSH1113:OrderBy|}(x => x);
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public IEnumerable<int> M(int[] values) => values.Order();
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an identity descending sort is flagged and rewritten to OrderDescending.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdentityOrderByDescendingIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public IEnumerable<int> M(int[] values) => values.{|PSH1113:OrderByDescending|}(x => x);
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public IEnumerable<int> M(int[] values) => values.OrderDescending();
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a trailing comparer argument survives the rewrite.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparerArgumentIsPreservedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public IEnumerable<string> M(string[] values)
                                      => values.{|PSH1113:OrderBy|}(x => x, System.StringComparer.Ordinal);
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public IEnumerable<string> M(string[] values)
                                           => values.Order(System.StringComparer.Ordinal);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a real key selector stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RealKeySelectorIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public IEnumerable<string> M(string[] values) => values.OrderBy(x => x.Length);
            }
            """);

    /// <summary>Verifies the rule stays silent on frameworks without Enumerable.Order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleIsGatedOnOrderExistingAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            TestCode = """
                       using System.Collections.Generic;
                       using System.Linq;

                       public class C
                       {
                           public IEnumerable<int> M(int[] values) => values.OrderBy(x => x);
                       }
                       """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string? fixedSource = null)
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
