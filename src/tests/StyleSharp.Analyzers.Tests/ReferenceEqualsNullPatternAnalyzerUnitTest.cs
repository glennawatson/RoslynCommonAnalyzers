// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyReferenceEquals = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2282ReferenceEqualsNullPatternAnalyzer,
    StyleSharp.Analyzers.Sst2282ReferenceEqualsNullPatternCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2282 (use an is-null pattern instead of a <c>ReferenceEquals</c> null check).</summary>
public class ReferenceEqualsNullPatternAnalyzerUnitTest
{
    /// <summary>Verifies <c>ReferenceEquals(value, null)</c> becomes <c>value is null</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullSecondArgumentBecomesIsNullAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(string value) => {|SST2282:ReferenceEquals(value, null)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(string value) => value is null;
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies the null-first argument order also becomes <c>value is null</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullFirstArgumentBecomesIsNullAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(string value) => {|SST2282:ReferenceEquals(null, value)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(string value) => value is null;
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a fully qualified <c>object.ReferenceEquals</c> call is reported and fixed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task QualifiedReferenceEqualsBecomesIsNullAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(object value) => {|SST2282:object.ReferenceEquals(value, null)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(object value) => value is null;
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a negated call becomes <c>value is not null</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NegatedReferenceEqualsBecomesIsNotNullAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(string value) => !{|SST2282:ReferenceEquals(value, null)|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(string value) => value is not null;
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies a value-type operand is left alone, because <c>ReferenceEquals</c> boxes it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ValueTypeOperandIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(int value) => ReferenceEquals(value, null);
                              }
                              """;
        await RunAsync(Source);
    }

    /// <summary>Verifies a same-named static method that is not <c>object.ReferenceEquals</c> is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnrelatedReferenceEqualsIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private static bool ReferenceEquals(string a, object b) => false;

                                  public bool M(string value) => ReferenceEquals(value, null);
                              }
                              """;
        await RunAsync(Source);
    }

    /// <summary>Verifies a call with no non-null operand is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BothNullOperandsAreCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M() => ReferenceEquals(null, null);
                              }
                              """;
        await RunAsync(Source);
    }

    /// <summary>Runs the analyzer and, when a fix is expected, the code fix against the given sources.</summary>
    /// <param name="source">The test source with markup.</param>
    /// <param name="fixedSource">The expected fixed source, or <see langword="null"/> when no fix is expected.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunAsync(string source, string? fixedSource = null)
    {
        var test = new VerifyReferenceEquals.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
            FixedCode = fixedSource ?? source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
