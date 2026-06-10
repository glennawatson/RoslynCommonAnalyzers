// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDuplicate = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1625DuplicateDocumentationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the duplicate documentation rule (SST1625).</summary>
public class DuplicateDocumentationAnalyzerUnitTest
{
    /// <summary>Verifies documentation copied between elements is reported (SST1625).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DuplicateDocumentationReportedAsync()
        => await VerifyDuplicate.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>The same text.</summary>
                /// {|SST1625:<param name="x">The same text.</param>|}
                public void M(int x)
                {
                }
            }
            """);

    /// <summary>Verifies distinct documentation text is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DistinctDocumentationIsCleanAsync()
        => await VerifyDuplicate.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>Does the work.</summary>
                /// <param name="x">The input value.</param>
                public void M(int x)
                {
                }
            }
            """);

    /// <summary>Verifies prose that matches but differs only in an inline cref reference is not flagged (SST1625).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParametersDifferingOnlyByCrefAreCleanAsync()
        => await VerifyDuplicate.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>A logger.</summary>
                /// <param name="writeNoType">A action which is called when the <see cref="Write(string)"/> is called.</param>
                /// <param name="writeWithType">A action which is called when the <see cref="Write(string, int)"/> is called.</param>
                /// <param name="writeWithException">A action which is called when the <see cref="Write(System.Exception, string)"/> is called.</param>
                public void M(int writeNoType, int writeWithType, int writeWithException)
                {
                }

                /// <summary>Writes the value.</summary>
                /// <param name="value">The value.</param>
                public void Write(string value)
                {
                }

                /// <summary>Writes the value with a count.</summary>
                /// <param name="value">The value.</param>
                /// <param name="count">The count.</param>
                public void Write(string value, int count)
                {
                }

                /// <summary>Writes the value with an exception.</summary>
                /// <param name="error">The error.</param>
                /// <param name="value">The value.</param>
                public void Write(System.Exception error, string value)
                {
                }
            }
            """);

    /// <summary>Verifies prose that matches and shares the same inline cref is still reported as a copy (SST1625).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParametersWithIdenticalProseAndCrefAreReportedAsync()
        => await VerifyDuplicate.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <summary>A logger.</summary>
                /// <param name="first">A action which is called when the <see cref="Write(string)"/> is called.</param>
                /// {|SST1625:<param name="second">A action which is called when the <see cref="Write(string)"/> is called.</param>|}
                public void M(int first, int second)
                {
                }

                /// <summary>Writes the value.</summary>
                /// <param name="value">The value.</param>
                public void Write(string value)
                {
                }
            }
            """);
}
