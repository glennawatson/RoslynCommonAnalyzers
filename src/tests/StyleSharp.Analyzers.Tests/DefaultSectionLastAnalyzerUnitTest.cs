// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDefault = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1219DefaultSectionLastAnalyzer>;
using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1219DefaultSectionLastAnalyzer,
    StyleSharp.Analyzers.Sst1219DefaultSectionLastCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1219 (a switch statement's default section is not last).</summary>
public class DefaultSectionLastAnalyzerUnitTest
{
    /// <summary>The switch with a leading default section.</summary>
    private const string LeadingDefaultSource = """
        public sealed class C
        {
            public int M(int x)
            {
                switch (x)
                {
                    {|SST1219:default|}:
                        return 0;
                    case 1:
                        return 1;
                }
            }
        }
        """;

    /// <summary>The switch after moving the default section last.</summary>
    private const string LeadingDefaultFixed = """
        public sealed class C
        {
            public int M(int x)
            {
                switch (x)
                {
                    case 1:
                        return 1;
                    default:
                        return 0;
                }
            }
        }
        """;

    /// <summary>Verifies a default section before a case is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingDefaultIsReportedAsync()
        => await VerifyDefault.VerifyAnalyzerAsync(LeadingDefaultSource);

    /// <summary>Verifies a trailing default section is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingDefaultIsCleanAsync()
        => await VerifyDefault.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            return 1;
                        default:
                            return 0;
                    }
                }
            }
            """);

    /// <summary>Verifies a switch with no default section is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoDefaultIsCleanAsync()
        => await VerifyDefault.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            return 1;
                        case 2:
                            return 2;
                    }

                    return -1;
                }
            }
            """);

    /// <summary>Verifies the fix moves the default section to the end.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixMovesDefaultToTheEndAsync()
        => await VerifyFix.VerifyCodeFixAsync(LeadingDefaultSource, LeadingDefaultFixed);
}
