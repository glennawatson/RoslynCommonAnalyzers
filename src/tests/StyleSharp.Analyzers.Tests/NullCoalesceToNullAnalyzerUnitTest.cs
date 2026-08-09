// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNullCoalesceToNull = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2453NullCoalesceToNullAnalyzer,
    StyleSharp.Analyzers.Sst2453NullCoalesceToNullCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2453NullCoalesceToNullAnalyzer"/> and its code fix (SST2453).</summary>
public class NullCoalesceToNullAnalyzerUnitTest
{
    /// <summary>Verifies coalescing to the null literal is reported and folded to the left operand.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullLiteralFallbackIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public string M(string text) => {|SST2453:text ?? null|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public string M(string text) => text;
                                   }
                                   """;
        await VerifyNullCoalesceToNull.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a parenthesized null fallback is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedNullFallbackIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public string M(string text) => {|SST2453:text ?? (null)|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public string M(string text) => text;
                                   }
                                   """;
        await VerifyNullCoalesceToNull.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a cast null fallback is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CastNullFallbackIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public string M(string text) => {|SST2453:text ?? (string)null|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public string M(string text) => text;
                                   }
                                   """;
        await VerifyNullCoalesceToNull.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a constant field whose value is null is reported as a fallback.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantNullFallbackIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private const string Missing = null;

                                  public string M(string text) => {|SST2453:text ?? Missing|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private const string Missing = null;

                                       public string M(string text) => text;
                                   }
                                   """;
        await VerifyNullCoalesceToNull.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a nullable value type coalesced to null is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableValueTypeFallbackIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public int? M(int? value) => {|SST2453:value ?? null|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int? M(int? value) => value;
                                   }
                                   """;
        await VerifyNullCoalesceToNull.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies coalescing a value with itself is reported and folded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelfCoalescingIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public string M(string text) => {|SST2453:text ?? text|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public string M(string text) => text;
                                   }
                                   """;
        await VerifyNullCoalesceToNull.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies coalescing a member chain with itself is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelfCoalescingMemberChainIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class Owner
                              {
                                  public string Name { get; set; } = "";
                              }

                              internal class C
                              {
                                  private Owner _owner = new Owner();

                                  public string M() => {|SST2453:this._owner.Name ?? this._owner.Name|};
                              }
                              """;
        const string FixedSource = """
                                   internal class Owner
                                   {
                                       public string Name { get; set; } = "";
                                   }

                                   internal class C
                                   {
                                       private Owner _owner = new Owner();

                                       public string M() => this._owner.Name;
                                   }
                                   """;
        await VerifyNullCoalesceToNull.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a repeated call is left alone; the two evaluations are not the same thing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RepeatedCallIsCleanAsync()
        => await VerifyNullCoalesceToNull.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static string Get() => "a";

                public string M() => Get() ?? Get();
            }
            """);

    /// <summary>Verifies a real fallback value is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RealFallbackIsCleanAsync()
        => await VerifyNullCoalesceToNull.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public string M(string text) => text ?? "anonymous";
            }
            """);

    /// <summary>Verifies a computed fallback is left alone without asking the model for a constant.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComputedFallbackIsCleanAsync()
        => await VerifyNullCoalesceToNull.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public string M(string text) => text ?? Compute();

                private static string Compute() => "computed";
            }
            """);

    /// <summary>Verifies a coalescing whose left is also a constant null is left to the rule that owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantNullOnBothSidesIsCleanAsync()
        => await VerifyNullCoalesceToNull.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private const string Missing = null;

                public string M() => Missing ?? null;
            }
            """);

    /// <summary>Verifies a fallback that widens the result type is left alone; folding would change the type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WideningNullFallbackIsCleanAsync()
        => await VerifyNullCoalesceToNull.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public long? M(int? value) => value ?? (long?)null;
            }
            """);

    /// <summary>Verifies a null-coalescing assignment is a different operator and is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CoalesceAssignmentIsCleanAsync()
        => await VerifyNullCoalesceToNull.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public string M(string text)
                {
                    text ??= "anonymous";
                    return text;
                }
            }
            """);
}
