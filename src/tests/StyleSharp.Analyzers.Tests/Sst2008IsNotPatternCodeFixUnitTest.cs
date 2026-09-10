// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyIsNotPatternFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2008IsNotPatternAnalyzer,
    StyleSharp.Analyzers.Sst2008IsNotPatternCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST2008 code fix (use an is-not pattern).</summary>
public class Sst2008IsNotPatternCodeFixUnitTest
{
    /// <summary>Verifies a negated property pattern becomes an is-not pattern.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NegatedPropertyPatternBecomesIsNotAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(object value) => {|SST2008:!(value is string { Length: 0 })|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(object value) => value is not string { Length: 0 };
                                   }
                                   """;
        await VerifyIsNotPatternFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a combined pattern is grouped when it goes under the negation.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <c>not</c> binds tighter than <c>or</c>, so an ungrouped rewrite reads as <c>(not 'a') or 'b'</c> —
    /// a different match, and the compiler rejects the later patterns as unreachable.
    /// </remarks>
    [Test]
    public async Task NegatedOrPatternIsGroupedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(char value) => {|SST2008:!(value is '{' or '(' or '<')|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(char value) => value is not ('{' or '(' or '<');
                                   }
                                   """;
        await VerifyIsNotPatternFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a grouped negation loses the group along with the negation.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NegatedGroupedPatternUngroupsAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(char value) => {|SST2008:!(value is not ('{' or '('))|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(char value) => value is '{' or '(';
                                   }
                                   """;
        await VerifyIsNotPatternFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an already-negated pattern loses its negation rather than gaining another.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DoubleNegationCollapsesAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(object value) => {|SST2008:!(value is not null)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(object value) => value is null;
                                   }
                                   """;
        await VerifyIsNotPatternFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the rewrite keeps its place inside a larger condition.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NegationInsideAConjunctionKeepsItsPlaceAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(int value, bool other) => other && {|SST2008:!(value is 1)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(int value, bool other) => other && value is not 1;
                                   }
                                   """;
        await VerifyIsNotPatternFix.VerifyCodeFixAsync(Source, FixedSource);
    }
}
