// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyGeneralException = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2409ThrowsGeneralExceptionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2409 (throwing a general exception type).</summary>
public class ThrowsGeneralExceptionAnalyzerUnitTest
{
    /// <summary>Verifies each of the three general types is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneralExceptionTypesAreReportedAsync()
        => await VerifyGeneralException.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void Broad() => throw new {|SST2409:Exception|}("broken");

                public void Runtime() => throw new {|SST2409:SystemException|}();

                public void Legacy() => throw new {|SST2409:ApplicationException|}();
            }
            """);

    /// <summary>Verifies a throw expression is reported like a throw statement.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowExpressionIsReportedAsync()
        => await VerifyGeneralException.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class C
            {
                public string M(string? value) => value ?? throw new {|SST2409:Exception|}("missing");
            }
            """);

    /// <summary>Verifies a fully qualified general type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedGeneralExceptionIsReportedAsync()
        => await VerifyGeneralException.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M() => throw new {|SST2409:System.Exception|}("broken");
            }
            """);

    /// <summary>Verifies a type that names the failure is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpecificExceptionIsCleanAsync()
        => await VerifyGeneralException.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class C
            {
                public void M(string? value)
                {
                    if (value is null)
                    {
                        throw new ArgumentNullException(nameof(value));
                    }

                    throw new InvalidOperationException("broken");
                }
            }
            """);

    /// <summary>Verifies a type of the project's own that derives from the general one is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Deriving from <c>Exception</c> is how a project names its failures, which is the fix, not the problem.</remarks>
    [Test]
    public async Task DerivedExceptionIsCleanAsync()
        => await VerifyGeneralException.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class OrderException : Exception
            {
                public OrderException(string message)
                    : base(message)
                {
                }
            }

            public sealed class C
            {
                public void M() => throw new OrderException("broken");
            }
            """);

    /// <summary>Verifies a rethrow is not a throw of a new exception.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RethrowIsCleanAsync()
        => await VerifyGeneralException.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(Action work)
                {
                    try
                    {
                        work();
                    }
                    catch (InvalidOperationException)
                    {
                        throw;
                    }
                }
            }
            """);
}
