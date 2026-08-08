// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyException = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1665ExceptionDescriptionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1665 (a documented exception should describe what triggers it).</summary>
public class ExceptionDescriptionAnalyzerUnitTest
{
    /// <summary>Verifies a paired exception element with no prose is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyPairedElementIsReportedAsync()
        => await VerifyException.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                /// <summary>Does the thing.</summary>
                /// <param name="count">The count.</param>
                /// {|SST1665:<exception cref="ArgumentOutOfRangeException"></exception>|}
                public void M(int count)
                {
                    if (count < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(count));
                    }
                }
            }
            """);

    /// <summary>Verifies a self-closing exception element is reported: it has nowhere to put a reason.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelfClosingElementIsReportedAsync()
        => await VerifyException.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                /// <summary>Does the thing.</summary>
                /// {|SST1665:<exception cref="ArgumentNullException" />|}
                public void M(string value) => throw new ArgumentNullException(nameof(value));
            }
            """);

    /// <summary>Verifies an element holding only whitespace across several lines is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WhitespaceOnlyElementIsReportedAsync()
        => await VerifyException.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                /// <summary>Does the thing.</summary>
                /// {|SST1665:<exception cref="InvalidOperationException">
                /// </exception>|}
                public void M() => throw new InvalidOperationException();
            }
            """);

    /// <summary>Verifies the reported name is the rightmost segment of a qualified cref.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedCrefIsReportedByItsSimpleNameAsync()
        => await VerifyException.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                /// <summary>Does the thing.</summary>
                /// {|SST1665:<exception cref="System.ArgumentOutOfRangeException"></exception>|}
                public void M() => throw new System.ArgumentOutOfRangeException();
            }
            """);

    /// <summary>Verifies an element that describes the trigger is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DescribedExceptionIsCleanAsync()
        => await VerifyException.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                /// <summary>Does the thing.</summary>
                /// <param name="count">The count.</param>
                /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
                public void M(int count)
                {
                    if (count < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(count));
                    }
                }
            }
            """);

    /// <summary>Verifies an element whose only content is a nested element is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElementDescribedByANestedElementIsCleanAsync()
        => await VerifyException.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                /// <summary>Does the thing.</summary>
                /// <exception cref="ArgumentNullException"><inheritdoc/></exception>
                public void M() => throw new ArgumentNullException();
            }
            """);

    /// <summary>Verifies an exception element without a cref is left to the malformed-documentation rules.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElementWithoutACrefIsCleanAsync()
        => await VerifyException.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                /// <summary>Does the thing.</summary>
                /// <exception></exception>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies an exception element nested inside another section is not the member's contract.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NestedExceptionElementIsCleanAsync()
        => await VerifyException.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                /// <summary>Does the thing.</summary>
                /// <remarks>
                /// The shape callers get wrong is <exception cref="ArgumentNullException"></exception>.
                /// </remarks>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies each empty element of a member documenting several exceptions is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryEmptyElementIsReportedAsync()
        => await VerifyException.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                /// <summary>Does the thing.</summary>
                /// {|SST1665:<exception cref="ArgumentNullException"></exception>|}
                /// <exception cref="InvalidOperationException">The connection is closed.</exception>
                /// {|SST1665:<exception cref="NotSupportedException" />|}
                public void M()
                {
                }
            }
            """);
}
