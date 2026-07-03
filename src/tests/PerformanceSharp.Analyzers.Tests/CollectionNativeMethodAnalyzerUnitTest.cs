// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyNativeMethod = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.CollectionNativeMethodAnalyzer,
    PerformanceSharp.Analyzers.CollectionNativeMethodCodeFixProvider>;
using VerifyNativeMethodAnalyzer = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.CollectionNativeMethodAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for the collection native-method rules (PSH1110, PSH1111) and their fixes.</summary>
public class CollectionNativeMethodAnalyzerUnitTest
{
    /// <summary>Verifies a List FirstOrDefault predicate call is reported (PSH1110) and renamed to Find.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListFirstOrDefaultReplacedWithFindAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(List<int> list) => list.{|PSH1110:FirstOrDefault|}(x => x > 1);
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(List<int> list) => list.Find(x => x > 1);
                                   }
                                   """;
        await VerifyFixNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a List All predicate call is reported (PSH1110) and renamed to TrueForAll.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListAllReplacedWithTrueForAllAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(List<int> list) => list.{|PSH1110:All|}(x => x > 1);
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(List<int> list) => list.TrueForAll(x => x > 1);
                                   }
                                   """;
        await VerifyFixNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a List Any predicate call is reported (PSH1110) and renamed to Exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListAnyReplacedWithExistsAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(List<int> list) => list.{|PSH1110:Any|}(x => x > 1);
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(List<int> list) => list.Exists(x => x > 1);
                                   }
                                   """;
        await VerifyFixNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a List Any equality predicate is a membership test (PSH1111), not PSH1110, and becomes Contains.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListAnyEqualityReplacedWithContainsAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(List<int> list, int y) => list.{|PSH1111:Any|}(x => x == y);
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(List<int> list, int y) => list.Contains(y);
                                   }
                                   """;
        await VerifyFixNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a HashSet Any equality predicate is reported (PSH1111) and becomes Contains.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HashSetAnyEqualityReplacedWithContainsAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(HashSet<int> hashSet, int y) => hashSet.{|PSH1111:Any|}(x => x == y);
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(HashSet<int> hashSet, int y) => hashSet.Contains(y);
                                   }
                                   """;
        await VerifyFixNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an array Any predicate call is reported (PSH1110) and rewritten to the static Array.Exists helper.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayAnyReplacedWithArrayExistsAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public bool M(int[] values) => values.{|PSH1110:Any|}(x => x > 1);
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(int[] values) => System.Array.Exists(values, x => x > 1);
                                   }
                                   """;
        await VerifyFixNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an array FirstOrDefault predicate call is reported (PSH1110) and rewritten to Array.Find.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayFirstOrDefaultReplacedWithArrayFindAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public int M(int[] values) => values.{|PSH1110:FirstOrDefault|}(x => x > 1);
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(int[] values) => System.Array.Find(values, x => x > 1);
                                   }
                                   """;
        await VerifyFixNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a method-call array receiver is still reported (PSH1110) even though no fix is offered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodCallArrayReceiverReportedWithoutFixAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public int[] Values() => new[] { 2, 3 };

                                  public bool M() => Values().{|PSH1110:Any|}(x => x > 1);
                              }
                              """;
        await VerifyAnalyzerNet90Async(Source);
    }

    /// <summary>Verifies a plain IEnumerable receiver is not reported by either rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumerableReceiverIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(IEnumerable<int> source, int y) => source.Any(x => x == y);

                                  public int N(IEnumerable<int> source) => source.FirstOrDefault(x => x > 1);
                              }
                              """;
        await VerifyFixNet90Async(Source, Source);
    }

    /// <summary>Verifies a parameterless Any() call is left to PSH1103 and never double-reported here.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterlessAnyIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(List<int> list) => list.Any();
                              }
                              """;
        await VerifyFixNet90Async(Source, Source);
    }

    /// <summary>Verifies an equality whose value side references the parameter stays PSH1110 Exists, not PSH1111.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelfEqualityOnListTreatedAsExistsAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(List<int> list) => list.{|PSH1110:Any|}(x => x == x);
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(List<int> list) => list.Exists(x => x == x);
                                   }
                                   """;
        await VerifyFixNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a statement-bodied Any lambda on a List still qualifies for PSH1110's Exists rename.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StatementBodiedAnyOnListReplacedWithExistsAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public bool M(List<int> list, int y) => list.{|PSH1110:Any|}(x => { return x == y; });
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public bool M(List<int> list, int y) => list.Exists(x => { return x == y; });
                                   }
                                   """;
        await VerifyFixNet90Async(Source, FixedSource);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixNet90Async(string source, string fixedSource)
    {
        var test = new VerifyNativeMethod.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAnalyzerNet90Async(string source)
    {
        var test = new VerifyNativeMethodAnalyzer.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
