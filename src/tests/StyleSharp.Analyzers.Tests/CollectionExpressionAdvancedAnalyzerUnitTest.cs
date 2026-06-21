// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyBuilderExpression = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.CollectionExpressionAdvancedAnalyzer,
    StyleSharp.Analyzers.CollectionExpressionBuilderCodeFixProvider>;

using VerifyCollectionExpression = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.CollectionExpressionAdvancedAnalyzer,
    StyleSharp.Analyzers.CollectionExpressionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for advanced collection-expression rules (SST2102-SST2105).</summary>
public class CollectionExpressionAdvancedAnalyzerUnitTest
{
    /// <summary>Verifies stackalloc initializers assigned to span targets can use collection expressions.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StackallocInitializerIsFixedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public int Sum()
                                  {
                                      Span<int> values = {|SST2102:stackalloc int[] { 1, 2, 3 }|};
                                      return values[0] + values[1] + values[2];
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public int Sum()
                                       {
                                           Span<int> values = [ 1, 2, 3 ];
                                           return values[0] + values[1] + values[2];
                                       }
                                   }
                                   """;
        var test = CreateNet80CollectionTest(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies collection-builder Create calls can use collection expressions.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BuilderCreateCallIsFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections;
                              using System.Collections.Generic;
                              using System.Runtime.CompilerServices;

                              [CollectionBuilder(typeof(MyCollection), "Create")]
                              public sealed class MyCollection<T> : IEnumerable<T>
                              {
                                  public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();
                                  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                              }

                              public static class MyCollection
                              {
                                  public static MyCollection<T> Create<T>(params T[] values) => throw new NotImplementedException();
                                  public static MyCollection<T> Create<T>(ReadOnlySpan<T> values) => throw new NotImplementedException();
                              }

                              public sealed class C
                              {
                                  private MyCollection<int> _values = {|SST2103:MyCollection.Create(1, 2, 3)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections;
                                   using System.Collections.Generic;
                                   using System.Runtime.CompilerServices;

                                   [CollectionBuilder(typeof(MyCollection), "Create")]
                                   public sealed class MyCollection<T> : IEnumerable<T>
                                   {
                                       public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();
                                       IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                                   }

                                   public static class MyCollection
                                   {
                                       public static MyCollection<T> Create<T>(params T[] values) => throw new NotImplementedException();
                                       public static MyCollection<T> Create<T>(ReadOnlySpan<T> values) => throw new NotImplementedException();
                                   }

                                   public sealed class C
                                   {
                                       private MyCollection<int> _values = [1, 2, 3];
                                   }
                                   """;
        var test = CreateNet80CollectionTest(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies fluent array conversions can use the target collection expression directly.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FluentArrayConversionIsFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public sealed class C
                              {
                                  private List<int> _values = {|SST2105:new[] { 1, 2, 3 }.ToList()|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       private List<int> _values = [ 1, 2, 3 ];
                                   }
                                   """;
        var test = CreateNet80CollectionTest(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a compact builder sequence can be replaced with a collection expression return.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BuilderSequenceIsFixedAsync()
    {
        const string Source = """
                              using System.Collections.Immutable;

                              public sealed class C
                              {
                                  public ImmutableArray<int> Values()
                                  {
                                      {|SST2104:var builder = ImmutableArray.CreateBuilder<int>();|}
                                      builder.Add(1);
                                      builder.Add(2);
                                      return builder.ToImmutable();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Immutable;

                                   public sealed class C
                                   {
                                       public ImmutableArray<int> Values()
                                       {
                                           return [1, 2];
                                       }
                                   }
                                   """;
        var test = new VerifyBuilderExpression.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies targetless and non-builder cases stay clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AmbiguousCollectionShapesAreCleanAsync()
    {
        const string Source = """
                              using System;
                              using System.Linq;

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      var values = new[] { 1, 2, 3 }.ToList();
                                  }
                              }
                              """;
        var test = CreateNet80CollectionTest(Source, Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a collection-expression verifier test targeting .NET 8.</summary>
    /// <param name="source">The source.</param>
    /// <param name="fixedSource">The fixed source.</param>
    /// <returns>The configured test.</returns>
    private static VerifyCollectionExpression.Test CreateNet80CollectionTest(string source, string fixedSource)
        => new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
            FixedCode = fixedSource
        };
}
