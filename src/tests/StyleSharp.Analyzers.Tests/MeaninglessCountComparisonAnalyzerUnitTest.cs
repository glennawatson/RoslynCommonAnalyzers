// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCountComparison = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1479MeaninglessCountComparisonAnalyzer,
    StyleSharp.Analyzers.Sst1479MeaninglessCountComparisonCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1479 (count and length comparisons should be satisfiable) and its fix.</summary>
public class MeaninglessCountComparisonAnalyzerUnitTest
{
    /// <summary>Verifies <c>Count &gt;= 0</c> is reported as always true and folded to the literal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountAtLeastZeroIsRewrittenToTrueAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  private readonly List<int> _items = new List<int>();

                                  public bool Any() => {|SST1479:_items.Count >= 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       private readonly List<int> _items = new List<int>();

                                       public bool Any() => true;
                                   }
                                   """;
        await VerifyCountComparison.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies <c>Count &lt; 0</c> is reported as always false and folded to the literal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CountBelowZeroIsRewrittenToFalseAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  private readonly List<int> _items = new List<int>();

                                  public bool Broken() => {|SST1479:_items.Count < 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       private readonly List<int> _items = new List<int>();

                                       public bool Broken() => false;
                                   }
                                   """;
        await VerifyCountComparison.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an array length compared for equality with a negative literal is always false.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayLengthEqualToNegativeIsRewrittenToFalseAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly int[] _values = new int[4];

                                  public bool Missing() => {|SST1479:_values.Length == -1|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private readonly int[] _values = new int[4];

                                       public bool Missing() => false;
                                   }
                                   """;
        await VerifyCountComparison.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a string length compared for inequality with a negative literal is always true.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringLengthNotEqualToNegativeIsRewrittenToTrueAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool Present(string text) => {|SST1479:text.Length != -1|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool Present(string text) => true;
                                   }
                                   """;
        await VerifyCountComparison.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the ordered comparisons against a negative literal are all decided.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OrderedComparisonsAgainstNegativeAreDecidedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool AtMost(string text) => {|SST1479:text.Length <= -1|};

                                  public bool Above(string text) => {|SST1479:text.Length > -1|};

                                  public bool Below(string text) => {|SST1479:text.Length < -2|};

                                  public bool AtLeast(string text) => {|SST1479:text.Length >= -2|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool AtMost(string text) => false;

                                       public bool Above(string text) => true;

                                       public bool Below(string text) => false;

                                       public bool AtLeast(string text) => true;
                                   }
                                   """;
        await VerifyCountComparison.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the literal may be written on either side of the operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralOnTheLeftIsReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  private readonly List<int> _items = new List<int>();

                                  public bool Any() => {|SST1479:0 <= _items.Count|};

                                  public bool Broken() => {|SST1479:0 > _items.Count|};

                                  public bool Present() => {|SST1479:-1 < _items.Count|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       private readonly List<int> _items = new List<int>();

                                       public bool Any() => true;

                                       public bool Broken() => false;

                                       public bool Present() => true;
                                   }
                                   """;
        await VerifyCountComparison.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a span length is recognized as a non-negative count.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpanLengthIsReportedAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public bool Any(Span<char> span) => {|SST1479:span.Length >= 0|};

                                  public bool Empty(ReadOnlySpan<byte> bytes) => {|SST1479:bytes.Length < 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public bool Any(Span<char> span) => true;

                                       public bool Empty(ReadOnlySpan<byte> bytes) => false;
                                   }
                                   """;
        await VerifyCountComparison.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the LINQ counting operator is recognized in both its extension and static forms.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumerableCountIsReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public sealed class C
                              {
                                  public bool Any(IEnumerable<int> items) => {|SST1479:items.Count() >= 0|};

                                  public bool Static(IEnumerable<int> items) => {|SST1479:Enumerable.Count(items) < 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public bool Any(IEnumerable<int> items) => true;

                                       public bool Static(IEnumerable<int> items) => false;
                                   }
                                   """;
        await VerifyCountComparison.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies every type whose <c>Count</c> satisfies a BCL collection interface is covered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The rule asks the interface, not the type, so a dictionary, a set and the interfaces themselves all follow.</remarks>
    [Test]
    public async Task CollectionInterfaceCountsAreReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public bool Map(Dictionary<string, int> map) => {|SST1479:map.Count >= 0|};

                                  public bool Set(HashSet<int> set) => {|SST1479:set.Count >= 0|};

                                  public bool Mutable(ICollection<int> items) => {|SST1479:items.Count >= 0|};

                                  public bool Readable(IReadOnlyCollection<int> items) => {|SST1479:items.Count >= 0|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public bool Map(Dictionary<string, int> map) => true;

                                       public bool Set(HashSet<int> set) => true;

                                       public bool Mutable(ICollection<int> items) => true;

                                       public bool Readable(IReadOnlyCollection<int> items) => true;
                                   }
                                   """;
        await VerifyCountComparison.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comparison that still asks a real question is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SatisfiableComparisonsAreCleanAsync()
        => await VerifyCountComparison.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                private readonly List<int> _items = new List<int>();

                private readonly int[] _values = new int[4];

                public bool Any() => _items.Count > 0;

                public bool Empty() => _items.Count == 0;

                public bool NotEmpty() => _items.Count != 0;

                public bool Pair() => _values.Length == 2;

                public bool Short(string text) => text.Length < 3;

                public bool Long(string text) => text.Length >= 8;
            }
            """);

    /// <summary>Verifies an operand that only looks like a count never reaches the semantic model's answer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OperandsThatAreNotCountsAreCleanAsync()
        => await VerifyCountComparison.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool Found(string text) => text.IndexOf('a') >= 0;

                public bool NonNegative(int value) => value >= 0;

                public bool Balanced(int left, int right) => left - right < 0;
            }
            """);

    /// <summary>Verifies a <c>Count</c> that satisfies no collection interface is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Nothing says a user-defined count is non-negative; a running balance is a perfectly good <c>Count</c>.</remarks>
    [Test]
    public async Task UserDefinedCountIsCleanAsync()
        => await VerifyCountComparison.VerifyAnalyzerAsync(
            """
            public sealed class Ledger
            {
                public int Count { get; set; }

                public int Length { get; set; }
            }

            public sealed class C
            {
                public bool Positive(Ledger ledger) => ledger.Count >= 0;

                public bool Broken(Ledger ledger) => ledger.Length < 0;
            }
            """);

    /// <summary>Verifies a count read through a conditional access is nullable, so the comparison still decides something.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAccessCountIsCleanAsync()
        => await VerifyCountComparison.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public bool Any(List<int> items) => items?.Count >= 0;
            }
            """);

    /// <summary>Verifies the document-based Fix All folds every occurrence in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllFoldsEveryOccurrenceAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  private readonly List<int> _items = new List<int>();

                                  public bool Any() => {|SST1479:_items.Count >= 0|};

                                  public bool Broken() => {|SST1479:_items.Count < 0|};

                                  public bool Missing(string text) => {|SST1479:text.Length == -1|};

                                  public bool Guarded(string text) => {|SST1479:0 <= text.Length|} && text.Length > 2;
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       private readonly List<int> _items = new List<int>();

                                       public bool Any() => true;

                                       public bool Broken() => false;

                                       public bool Missing(string text) => false;

                                       public bool Guarded(string text) => true && text.Length > 2;
                                   }
                                   """;
        await VerifyCountComparison.VerifyCodeFixAsync(Source, FixedSource);
    }
}
