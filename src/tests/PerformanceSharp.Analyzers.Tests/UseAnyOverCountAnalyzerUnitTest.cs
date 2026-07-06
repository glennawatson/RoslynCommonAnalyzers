// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyAny = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1119UseAnyOverCountAnalyzer,
    PerformanceSharp.Analyzers.Psh1119UseAnyOverCountCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1119 (check for elements without counting them all) and its code fix.</summary>
public class UseAnyOverCountAnalyzerUnitTest
{
    /// <summary>Verifies Count() &gt; 0 on a plain enumerable is reported (PSH1119) and fixed to Any().</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountGreaterThanZeroReplacedWithAnyAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(IEnumerable<int> xs) => {|PSH1119:xs.Count() > 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(IEnumerable<int> xs) => xs.Any();
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Count() == 0 is reported and fixed to a negated Any().</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountEqualsZeroReplacedWithNegatedAnyAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(IEnumerable<int> xs) => {|PSH1119:xs.Count() == 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(IEnumerable<int> xs) => !xs.Any();
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the reversed operand order 0 &lt; Count() is reported and fixed to Any().</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroLessThanCountReplacedWithAnyAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(IEnumerable<int> xs) => {|PSH1119:0 < xs.Count()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(IEnumerable<int> xs) => xs.Any();
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Count() &gt;= 1 is reported and fixed to Any().</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountGreaterOrEqualOneReplacedWithAnyAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(IEnumerable<int> xs) => {|PSH1119:xs.Count() >= 1|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(IEnumerable<int> xs) => xs.Any();
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Count() &lt; 1 is reported and fixed to a negated Any().</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountLessThanOneReplacedWithNegatedAnyAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(IEnumerable<int> xs) => {|PSH1119:xs.Count() < 1|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(IEnumerable<int> xs) => !xs.Any();
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the predicate Count overload carries its predicate into the Any() fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PredicateCountNotEqualsZeroCarriesPredicateAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(IEnumerable<int> xs) => {|PSH1119:xs.Count(x => x > 2) != 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(IEnumerable<int> xs) => xs.Any(x => x > 2);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every reported comparison in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class A
                              {
                                  public bool M(IEnumerable<int> xs) => {|PSH1119:xs.Count() > 0|};
                              }

                              public class B
                              {
                                  public bool M(IEnumerable<int> xs) => {|PSH1119:xs.Count() == 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class A
                                   {
                                       public bool M(IEnumerable<int> xs) => xs.Any();
                                   }

                                   public class B
                                   {
                                       public bool M(IEnumerable<int> xs) => !xs.Any();
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a receiver with an O(1) Count property is not reported — that receiver is PSH1103's territory.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListReceiverIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(List<int> list) => list.Count() > 0;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies the predicate form is reported even on a counted receiver, where Any's early exit still wins.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PredicateCountOnListReceiverReplacedWithAnyAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(List<int> list) => {|PSH1119:list.Count(x => x > 2) > 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(List<int> list) => list.Any(x => x > 2);
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a comparison against a larger literal is not reported because the real count is needed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountAgainstLargerLiteralIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(IEnumerable<int> xs) => xs.Count() > 5;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies Count() &gt;= 2 is not reported because it asks for more than emptiness.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountGreaterOrEqualTwoIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(IEnumerable<int> xs) => xs.Count() >= 2;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies an instance Count() method on a custom type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceCountMethodIsCleanAsync()
    {
        const string Source = """
                              public sealed class Tally
                              {
                                  public int Count() => 0;
                              }

                              public class C
                              {
                                  public bool M(Tally tally) => tally.Count() > 0;
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyAny.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }
}
