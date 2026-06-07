// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Tests;

/// <summary>Helper-level tests for element-spacing hot-path predicates.</summary>
public sealed class ElementSpacingAnalyzerUnitTest
{
    /// <summary>Verifies adjacent start/end lines report missing blank-line separation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShouldReportSpacingWhenMembersTouchOrDifferByOneLineAsync()
    {
        await Assert.That(ElementSpacingAnalyzer.ShouldReportSpacing(previousEndLine: 2, currentStartLine: 3)).IsTrue();
        await Assert.That(ElementSpacingAnalyzer.ShouldReportSpacing(previousEndLine: 2, currentStartLine: 2)).IsTrue();
    }

    /// <summary>Verifies a blank intervening line stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShouldReportSpacingSkipsBlankLineSeparatedMembersAsync()
        => await Assert.That(ElementSpacingAnalyzer.ShouldReportSpacing(previousEndLine: 2, currentStartLine: 4)).IsFalse();
}
