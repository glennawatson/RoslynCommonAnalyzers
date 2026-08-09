// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyMissingAttributeUsage = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2336MissingAttributeUsageAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2336MissingAttributeUsageAnalyzer"/> (SST2336).</summary>
public class MissingAttributeUsageAnalyzerUnitTest
{
    /// <summary>Verifies an attribute type with no declared usage is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AttributeWithoutUsageIsFlaggedAsync()
        => await VerifyMissingAttributeUsage.VerifyAnalyzerAsync(
            """
            using System;

            internal sealed class {|SST2336:MarkerAttribute|} : Attribute
            {
            }
            """);

    /// <summary>Verifies an attribute declaring its usage is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AttributeWithUsageIsCleanAsync()
        => await VerifyMissingAttributeUsage.VerifyAnalyzerAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Method)]
            internal sealed class MarkerAttribute : Attribute
            {
            }
            """);

    /// <summary>Verifies usage inherited from a base attribute counts.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritedUsageIsCleanAsync()
        => await VerifyMissingAttributeUsage.VerifyAnalyzerAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Method)]
            internal abstract class BaseMarkerAttribute : Attribute
            {
            }

            internal sealed class MarkerAttribute : BaseMarkerAttribute
            {
            }
            """);

    /// <summary>Verifies an abstract attribute base is left to its concrete derivations.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractAttributeIsCleanAsync()
        => await VerifyMissingAttributeUsage.VerifyAnalyzerAsync(
            """
            using System;

            internal abstract class BaseMarkerAttribute : Attribute
            {
            }
            """);

    /// <summary>Verifies an ordinary type is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonAttributeTypeIsCleanAsync()
        => await VerifyMissingAttributeUsage.VerifyAnalyzerAsync(
            """
            internal sealed class Marker
            {
            }
            """);
}
