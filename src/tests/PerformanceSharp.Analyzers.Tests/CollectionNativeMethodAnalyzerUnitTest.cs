// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
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

    /// <summary>Verifies a method-call array receiver is not reported, because no fix can be offered for it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodCallArrayReceiverIsCleanAsync()
    {
        const string Source = """
                              using System.Linq;

                              public class C
                              {
                                  public int[] Values() => new[] { 2, 3 };

                                  public bool M() => Values().Any(x => x > 1);
                              }
                              """;
        await VerifyAnalyzerNet90Async(Source);
    }

    /// <summary>Verifies an awaited WhenAll array polled inline is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitedArrayReceiverIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<bool> M(List<int> list)
                                      => (await Task.WhenAll(list.Select(async x => new { Value = x, Ok = await Probe(x) }))).Any(p => p.Ok);

                                  private static Task<bool> Probe(int value) => Task.FromResult(value > 1);
                              }
                              """;
        await VerifyAnalyzerNet90Async(Source);
    }

    /// <summary>Verifies an awaited WhenAll array held in a local is still reported, because the fix applies.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitedArrayInLocalReplacedWithArrayExistsAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task<bool> M(List<int> list)
                                  {
                                      var results = await Task.WhenAll(list.Select(async x => new { Value = x, Ok = await Probe(x) }));
                                      return results.{|PSH1110:Any|}(p => p.Ok);
                                  }

                                  private static Task<bool> Probe(int value) => Task.FromResult(value > 1);
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task<bool> M(List<int> list)
                                       {
                                           var results = await Task.WhenAll(list.Select(async x => new { Value = x, Ok = await Probe(x) }));
                                           return System.Array.Exists(results, p => p.Ok);
                                       }

                                       private static Task<bool> Probe(int value) => Task.FromResult(value > 1);
                                   }
                                   """;
        await VerifyFixNet90Async(Source, FixedSource);
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

    /// <summary>Verifies complex equality operands preserve parameter dependence and support reversed membership tests.</summary>
    /// <param name="predicate">The predicate with expected diagnostic markup on its call.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("list.{|PSH1110:Any|}(x => x == y + x)")]
    [Arguments("list.{|PSH1111:Any|}(x => x == y + 1)")]
    [Arguments("list.{|PSH1111:Any|}((int x) => y == x)")]
    [Arguments("list.{|PSH1110:Any|}(x => y == 1)")]
    [Arguments("list.{|PSH1110:Any|}(x => x != y)")]
    public Task EqualityOperandDeterminesReplacementAsync(string predicate) =>
        VerifyAnalyzerNet90Async($"using System.Linq; using System.Collections.Generic; class C {{ bool M(List<int> list, int y) => {predicate}; }}");

    /// <summary>Verifies inherited and interface membership methods are found even when not declared by the receiver.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InheritedAndInterfaceContainsAreReportedAsync() =>
        VerifyAnalyzerNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;
            interface IValues : ICollection<int> { }
            class Values : List<int> { }
            class C
            {
                bool Inherited(Values values) => values.{|PSH1111:Any|}(x => x == 1);
                bool Interface(IValues values) => values.{|PSH1111:Any|}(x => x == 1);
            }
            """);

    /// <summary>Verifies membership on a type parameter is unreported even with a collection constraint.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstrainedGenericReceiverIsCleanAsync() =>
        VerifyAnalyzerNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;
            class C { bool M<T>(T values) where T : ICollection<int> => values.Any(x => x == 1); }
            """);

    /// <summary>Verifies a same-named member must have the public instance Boolean membership signature.</summary>
    /// <param name="member">The nonqualifying member declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("public bool Contains;")]
    [Arguments("public static bool Contains(int value) => false;")]
    [Arguments("private bool Contains(int value) => false;")]
    [Arguments("public int Contains(int value) => 0;")]
    [Arguments("public bool Contains() => false;")]
    [Arguments("public bool Contains(string value) => false;")]
    public Task IncompatibleContainsMemberIsCleanAsync(string member) =>
        VerifyAnalyzerNet90Async(
            $$"""
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq;
            class Values : IEnumerable<int>
            {
                {{member}}
                public IEnumerator<int> GetEnumerator() => throw new System.NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }
            class C { bool M(Values values) => values.Any(x => x == 1); }
            """);

    /// <summary>Verifies collection names are matched by their entire namespace.</summary>
    /// <param name="container">The namespace that contains the lookalike collection.</param>
    /// <param name="typeName">The native collection name being imitated.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Other", "List")]
    [Arguments("Other.Generic", "List")]
    [Arguments("Other.Collections.Generic", "List")]
    [Arguments("Other.System.Collections.Generic", "List")]
    [Arguments("Other", "ImmutableList")]
    [Arguments("Other.Immutable", "ImmutableList")]
    [Arguments("Other.Collections.Immutable", "ImmutableList")]
    [Arguments("Other.System.Collections.Immutable", "ImmutableList")]
    public Task CollectionNamespaceLookalikesAreCleanAsync(string container, string typeName) =>
        VerifyAnalyzerNet90Async(
            $$"""
            using System.Linq;
            namespace {{container}}
            {
                class {{typeName}}<T> : global::System.Collections.Generic.List<T> { }
                class C { bool M({{typeName}}<int> values) => values.Any(x => x > 1); }
            }
            """);

    /// <summary>Verifies immutable lists expose the same predicate replacements as mutable lists.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImmutableListPredicateIsReportedAsync() =>
        VerifyAnalyzerNet90Async(
            """
            using System.Linq;
            using System.Collections.Immutable;
            class C { bool M(ImmutableList<int> values) => values.{|PSH1110:Any|}(x => x > 1); }
            """);

    /// <summary>Verifies an array accessed through a member chain can use the static universal predicate helper.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArrayMemberAllBecomesTrueForAllAsync() =>
        VerifyFixNet90Async(
            "using System.Linq; class C { int[] values; bool M() => this.values.{|PSH1110:All|}(x => x > 0); }",
            "using System.Linq; class C { int[] values; bool M() => System.Array.TrueForAll(this.values, x => x > 0); }");

    /// <summary>Verifies invocations outside the supported extension-lambda shape stay unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnsupportedInvocationShapesHaveNoFixAsync() =>
        VerifyNativeMethod.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            class C
            {
                bool M(List<int> values, Func<int, bool> predicate)
                {
                    _ = values.Any<int>(x => x > 0);
                    _ = values.Any(predicate);
                    _ = values?.Any(x => x > 0);
                    _ = Enumerable.Any(values, x => x > 0);
                    _ = values.Count(x => x > 0);
                    return Any(x => x > 0);
                }
                bool Any(Func<int, bool> predicate) => false;
                bool N(C other) => other.Any(x => x > 0);
            }
            """);

    /// <summary>Verifies unusual Enumerable overloads do not imply unsupported array or membership replacements.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonGenericAndMultidimensionalEnumerableOverloadsAreCleanAsync() =>
        VerifyAnalyzerNet90Async(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            namespace System.Linq
            {
                static class Enumerable
                {
                    public static bool Any(this int[,] values, Func<int, bool> predicate) => false;
                    public static bool Any(this HashSet<int> values, Func<int, bool> predicate) => false;
                }
            }
            class C
            {
                bool Array(int[,] values) => values.Any(x => x > 0);
                bool Set(HashSet<int> values) => values.Any(x => x == 1);
            }
            """);

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixNet90Async(string source, string fixedSource)
    {
        var test = new VerifyNativeMethod.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAnalyzerNet90Async(string source)
    {
        var test = new VerifyNativeMethodAnalyzer.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source };

        await test.RunAsync(CancellationToken.None);
    }
}
