// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1307VolatileInterlockedFieldAnalyzer,
    PerformanceSharp.Analyzers.Psh1307VolatileInterlockedFieldCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1307VolatileInterlockedFieldAnalyzer"/> (PSH1307 volatile interlocked fields).</summary>
public class VolatileInterlockedFieldAnalyzerUnitTest
{
    /// <summary>Verifies a plain read of an interlocked field is flagged and wrapped in Volatile.Read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainReadIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class C
                              {
                                  private int _count;

                                  public void Add() => Interlocked.Increment(ref _count);

                                  public int Count => {|PSH1307:_count|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public class C
                                   {
                                       private int _count;

                                       public void Add() => Interlocked.Increment(ref _count);

                                       public int Count => Volatile.Read(ref _count);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a plain write of an interlocked field is flagged and wrapped in Volatile.Write.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainWriteIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public class C
                              {
                                  private long _total;

                                  public void Add(long value) => Interlocked.Add(ref _total, value);

                                  public void Reset()
                                  {
                                      {|PSH1307:_total|} = 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public class C
                                   {
                                       private long _total;

                                       public void Add(long value) => Interlocked.Add(ref _total, value);

                                       public void Reset()
                                       {
                                           Volatile.Write(ref _total, 0);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies constructor initialization stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorWriteIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading;

            public class C
            {
                private int _count;

                public C(int seed)
                {
                    _count = seed;
                }

                public void Add() => Interlocked.Increment(ref _count);
            }
            """);

    /// <summary>Verifies fields never touched by Interlocked stay clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedFieldIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading;

            public class C
            {
                private int _count;
                private int _plain;

                public void Add() => Interlocked.Increment(ref _count);

                public int Plain
                {
                    get => _plain;
                    set => _plain = value;
                }
            }
            """);

    /// <summary>Verifies a read through a readonly member stays clean — the Volatile fix can't take a ref there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadonlyMemberReadIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading;

            public struct C
            {
                private int _count;

                public void Add() => Interlocked.Increment(ref _count);

                public readonly int Snapshot() => _count;

                public readonly int Count => _count;
            }
            """);

    /// <summary>Verifies a read through a whole-property readonly block accessor stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadonlyPropertyBlockAccessorIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading;

            public struct C
            {
                private int _count;

                public void Add() => Interlocked.Increment(ref _count);

                public readonly int Count
                {
                    get => _count;
                }
            }
            """);

    /// <summary>Verifies the reported false positive — readonly equality/hash members reading an interlocked field.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadonlyEqualityMembersAreCleanAsync()
        => await VerifyAsync(
            """
            #nullable enable
            using System;
            using System.Threading;

            public struct Cell : IEquatable<Cell>
            {
                private object? _value;

                public void Publish(object v) => Interlocked.CompareExchange(ref _value, v, null);

                public readonly bool Equals(Cell other) => ReferenceEquals(_value, other._value);

                public override readonly int GetHashCode() => _value?.GetHashCode() ?? 0;

                public override readonly bool Equals(object? obj) => obj is Cell c && Equals(c);
            }
            """);

    /// <summary>Verifies a non-readonly struct member is still flagged and fixed — the guard is readonly-only.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonReadonlyStructMemberIsFlaggedAsync()
    {
        const string Source = """
                              using System.Threading;

                              public struct C
                              {
                                  private int _count;

                                  public void Add() => Interlocked.Increment(ref _count);

                                  public int Snapshot() => {|PSH1307:_count|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading;

                                   public struct C
                                   {
                                       private int _count;

                                       public void Add() => Interlocked.Increment(ref _count);

                                       public int Snapshot() => Volatile.Read(ref _count);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies accesses inside a lock stay clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LockedAccessIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Threading;

            public class C
            {
                private readonly object _gate = new object();
                private int _count;

                public void Add() => Interlocked.Increment(ref _count);

                public int Drain()
                {
                    lock (_gate)
                    {
                        return _count;
                    }
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
