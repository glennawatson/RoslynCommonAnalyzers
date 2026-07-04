// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1005ValueTypeEqualityBoxesAnalyzer,
    PerformanceSharp.Analyzers.Psh1005ValueTypeEqualityCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>
/// Tests for <see cref="Psh1005ValueTypeEqualityCodeFixProvider"/> — the multi-suggestion
/// fixes for PSH1005: record struct conversion, IEquatable implementation, and the readonly
/// combination.
/// </summary>
public class ValueTypeEqualityCodeFixUnitTest
{
    /// <summary>Verifies the record struct action converts an immutable struct to a readonly record struct.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordActionConvertsToReadonlyRecordStructAsync()
    {
        const string Source = """
                              public struct {|PSH1005:Token|}
                              {
                                  private readonly int _id;

                                  public Token(int id) => _id = id;

                                  public int Id => _id;
                              }
                              """;
        const string FixedSource = """
                                   public readonly record struct Token
                                   {
                                       private readonly int _id;

                                       public Token(int id) => _id = id;

                                       public int Id => _id;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource, "Psh1005ValueTypeEqualityCodeFixProvider.Record");
    }

    /// <summary>Verifies the record struct action keeps a mutable struct non-readonly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordActionKeepsMutableStructPlainAsync()
    {
        const string Source = """
                              public struct {|PSH1005:Counter|}
                              {
                                  public int Count;
                              }
                              """;
        const string FixedSource = """
                                   public record struct Counter
                                   {
                                       public int Count;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource, "Psh1005ValueTypeEqualityCodeFixProvider.Record");
    }

    /// <summary>Verifies the equatable action generates the full member set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EquatableActionGeneratesMembersAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public struct {|PSH1005:Counter|}
                              {
                                  public int Count;
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public struct Counter : IEquatable<Counter>
                                   {
                                       public int Count;

                                       public bool Equals(Counter other) => EqualityComparer<int>.Default.Equals(Count, other.Count);

                                       public override bool Equals(object obj) => obj is Counter other && Equals(other);

                                       public override int GetHashCode() => HashCode.Combine(Count);

                                       public static bool operator ==(Counter left, Counter right) => left.Equals(right);

                                       public static bool operator !=(Counter left, Counter right) => !left.Equals(right);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource, "Psh1005ValueTypeEqualityCodeFixProvider.Equatable");
    }

    /// <summary>Verifies the combined action implements the interface and makes the struct readonly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EquatableReadonlyActionDoesBothAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public struct {|PSH1005:Token|}
                              {
                                  private readonly int _id;

                                  public Token(int id) => _id = id;

                                  public int Id => _id;
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public readonly struct Token : IEquatable<Token>
                                   {
                                       private readonly int _id;

                                       public Token(int id) => _id = id;

                                       public int Id => _id;

                                       public bool Equals(Token other) => EqualityComparer<int>.Default.Equals(_id, other._id);

                                       public override bool Equals(object obj) => obj is Token other && Equals(other);

                                       public override int GetHashCode() => HashCode.Combine(_id);

                                       public static bool operator ==(Token left, Token right) => left.Equals(right);

                                       public static bool operator !=(Token left, Token right) => !left.Equals(right);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource, "Psh1005ValueTypeEqualityCodeFixProvider.EquatableReadonly");
    }

    /// <summary>Runs a code fix verification selecting one of the registered actions.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <param name="equivalenceKey">The action to apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource, string equivalenceKey)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource,
            CodeActionEquivalenceKey = equivalenceKey,
        };
        await test.RunAsync(CancellationToken.None);
    }
}
