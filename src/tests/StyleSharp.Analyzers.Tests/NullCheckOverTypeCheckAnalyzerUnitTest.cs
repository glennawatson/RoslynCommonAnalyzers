// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2019NullCheckOverTypeCheckAnalyzer,
    StyleSharp.Analyzers.Sst2019NullCheckOverTypeCheckCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2019 (a test against object that is really a null check).</summary>
public class NullCheckOverTypeCheckAnalyzerUnitTest
{
    /// <summary>Verifies 'x is object' is reported and rewritten as the non-null test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsObjectRewrittenAsNotNullAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(string value) => {|SST2019:value is object|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(string value) => value is not null;
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies 'x is not object' is reported and rewritten as the null test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNotObjectRewrittenAsNullAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(string value) => {|SST2019:value is not object|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(string value) => value is null;
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a nullable value type is reported, since it has a null to test for.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableValueTypeReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M(int? value) => {|SST2019:value is object|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M(int? value) => value is not null;
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a non-nullable value type is left alone, where the test is a constant.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonNullableValueTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(int value) => value is object;
            }
            """);

    /// <summary>Verifies a test against some other type is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherTypeTestIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object value) => value is string;
            }
            """);

    /// <summary>Verifies a declaration pattern that binds the value is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeclarationPatternIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(string value) => value is object bound && bound is not null;
            }
            """);

    /// <summary>Verifies an unconstrained generic is reported, since it may hold null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnconstrainedGenericReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public bool M<T>(T value) => {|SST2019:value is object|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public bool M<T>(T value) => value is not null;
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }
}
