// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyArrayCovariance = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2434ArrayCovarianceAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2434 (a reference-type array widened to an array of its base type).</summary>
public class Sst2434ArrayCovarianceAnalyzerUnitTest
{
    /// <summary>Verifies widening <c>string[]</c> to <c>object[]</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringArrayToObjectArrayIsReportedAsync()
        => await VerifyArrayCovariance.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public object[] Items = {|SST2434:new string[3]|};
            }
            """);

    /// <summary>Verifies widening at an assignment inside a method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignmentInMethodIsReportedAsync()
        => await VerifyArrayCovariance.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public object[] M(string[] source)
                {
                    return {|SST2434:source|};
                }
            }
            """);

    /// <summary>Verifies keeping the concrete array type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameElementTypeIsCleanAsync()
        => await VerifyArrayCovariance.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string[] Items = new string[3];
            }
            """);

    /// <summary>Verifies a read-only-list destination is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyListDestinationIsCleanAsync()
        => await VerifyArrayCovariance.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public IReadOnlyList<object> Items = new string[3];
            }
            """);

    /// <summary>Verifies a value-type element array is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueTypeElementIsCleanAsync()
        => await VerifyArrayCovariance.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int[] Items = new int[3];
            }
            """);

    /// <summary>Verifies an object array assigned an object array is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectArrayToObjectArrayIsCleanAsync()
        => await VerifyArrayCovariance.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public object[] Items = new object[3];
            }
            """);
}
