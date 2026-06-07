// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.MemberDocumentationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the documentation quality rules (SST1608/1612/1613/1614/1616/1620/1621/1622).</summary>
public class DocumentationQualityUnitTest
{
    /// <summary>Verifies a placeholder summary is reported (SST1608).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultSummaryAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// {|SST1608:<summary>Summary description here.</summary>|}
            public class Widget { }
            """);

    /// <summary>Verifies an extra parameter documentation element is reported (SST1612).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterMatchAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                /// <param name="value">The value.</param>
                /// {|SST1612:<param name="extra">Extra.</param>|}
                public void M(int value) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a nameless parameter documentation element is reported (SST1613).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterNameAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A widget.</summary>
            /// {|SST1613:<param>No name.</param>|}
            public class Widget { }
            """);

    /// <summary>Verifies an empty parameter documentation element is reported (SST1614).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterTextAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                /// <param name="value"></param>
                public void M(int {|SST1614:value|}) { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies an empty return documentation element is reported (SST1616).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnTextAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Gets a value.</summary>
                /// {|SST1616:<returns></returns>|}
                public int M() => 0;
            }
            """;

        await Verify.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies an extra type parameter documentation element is reported (SST1620).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterMatchAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                /// <typeparam name="T">The type.</typeparam>
                /// {|SST1620:<typeparam name="X">Extra.</typeparam>|}
                public void M<T>() { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a nameless type parameter documentation element is reported (SST1621).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterNameAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            /// <summary>A widget.</summary>
            /// {|SST1621:<typeparam>No name.</typeparam>|}
            public class Widget { }
            """);

    /// <summary>Verifies an empty type parameter documentation element is reported (SST1622).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterTextAsync()
    {
        const string Source = """
            /// <summary>A container.</summary>
            public class C
            {
                /// <summary>Does a thing.</summary>
                /// <typeparam name="T"></typeparam>
                public void M<{|SST1622:T|}>() { }
            }
            """;

        await Verify.VerifyAnalyzerAsync(Source);
    }
}
