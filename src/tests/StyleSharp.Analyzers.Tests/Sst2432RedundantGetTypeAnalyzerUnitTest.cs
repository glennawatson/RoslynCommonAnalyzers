// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2432RedundantGetTypeAnalyzer,
    StyleSharp.Analyzers.Sst2432RedundantGetTypeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2432 (GetType() called on a value that is already a Type).</summary>
public class Sst2432RedundantGetTypeAnalyzerUnitTest
{
    /// <summary>A GetType() call on a Type parameter.</summary>
    private const string TypeParameterSource = """
        using System;

        public sealed class C
        {
            public string Name(Type type) => {|SST2432:type.GetType()|}.Name;
        }
        """;

    /// <summary>The Type parameter case after the fix.</summary>
    private const string TypeParameterFixed = """
        using System;

        public sealed class C
        {
            public string Name(Type type) => type.Name;
        }
        """;

    /// <summary>A GetType() call on a typeof expression.</summary>
    private const string TypeofSource = """
        using System;

        public sealed class C
        {
            public Type M() => {|SST2432:typeof(string).GetType()|};
        }
        """;

    /// <summary>The typeof case after the fix.</summary>
    private const string TypeofFixed = """
        using System;

        public sealed class C
        {
            public Type M() => typeof(string);
        }
        """;

    /// <summary>Verifies GetType() on a Type parameter is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetTypeOnTypeParameterIsReportedAsync()
        => await Verify.VerifyCodeFixAsync(TypeParameterSource, TypeParameterFixed);

    /// <summary>Verifies GetType() on a typeof expression is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetTypeOnTypeofIsReportedAsync()
        => await Verify.VerifyCodeFixAsync(TypeofSource, TypeofFixed);

    /// <summary>Verifies GetType() on an ordinary object is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetTypeOnObjectIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public Type M(object value) => value.GetType();
            }
            """);

    /// <summary>Verifies GetType() on a string is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetTypeOnStringIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public Type M(string value) => value.GetType();
            }
            """);
}
