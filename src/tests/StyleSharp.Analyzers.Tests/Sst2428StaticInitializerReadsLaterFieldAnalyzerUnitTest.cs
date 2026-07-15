// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2428StaticInitializerReadsLaterFieldAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2428 (a static field initializer reads a static field declared later).</summary>
public class Sst2428StaticInitializerReadsLaterFieldAnalyzerUnitTest
{
    /// <summary>Verifies an initializer that reads a later static field of the same type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadsLaterFieldIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public static class C
            {
                public static readonly string Full = "root" + {|SST2428:Suffix|};
                public static readonly string Suffix = "/v1";
            }
            """);

    /// <summary>Verifies reading a static field declared earlier is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadsEarlierFieldIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public static class C
            {
                public static readonly string Suffix = "/v1";
                public static readonly string Full = "root" + Suffix;
            }
            """);

    /// <summary>Verifies reading a later constant is left alone: a constant has no ordering to get wrong.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadsLaterConstIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public static class C
            {
                public static readonly string Full = "root" + Suffix;
                public const string Suffix = "/v1";
            }
            """);

    /// <summary>Verifies reading a later static field of a different type is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadsLaterFieldOfDifferentTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public static class C
            {
                public static readonly string Full = "root" + Other.Suffix;
            }

            public static class Other
            {
                public static readonly string Suffix = "/v1";
            }
            """);

    /// <summary>Verifies an instance field initializer that reads a later static field is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceFieldInitializerIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _seed = Later;
                private static int Later = 5;
            }
            """);

    /// <summary>Verifies a static field with no initializer is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoInitializerIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public static class C
            {
                public static int Full;
                public static readonly int Suffix = 5;
            }
            """);

    /// <summary>Verifies an initializer that reads a later field from another part of a partial type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadsFieldFromOtherPartialFileIsReportedAsync()
    {
        var test = new Verify.Test();
        test.TestState.Sources.Add(("Part1.cs", """
            public static partial class C
            {
                public static readonly string Full = "root" + {|SST2428:Suffix|};
            }
            """));
        test.TestState.Sources.Add(("Part2.cs", """
            public static partial class C
            {
                public static readonly string Suffix = "/v1";
            }
            """));
        await test.RunAsync(CancellationToken.None);
    }
}
