// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEmptyCatch = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.ExceptionHandlingAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1429 (empty catch of the base exception).</summary>
public class EmptyCatchOfBaseExceptionAnalyzerUnitTest
{
    /// <summary>Verifies an empty <c>catch (Exception)</c> and a bare empty <c>catch</c> are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyBaseCatchesReportedAsync()
        => await VerifyEmptyCatch.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void Typed()
                {
                    try
                    {
                    }
                    {|SST1429:catch|} (Exception)
                    {
                    }
                }

                public void Bare()
                {
                    try
                    {
                    }
                    {|SST1429:catch|}
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a handled catch, a narrow catch, and a filtered catch are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HandledOrNarrowCatchesAreCleanAsync()
        => await VerifyEmptyCatch.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void M()
                {
                    try
                    {
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine(ex);
                    }

                    try
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    try
                    {
                    }
                    catch (Exception) when (System.Environment.HasShutdownStarted)
                    {
                    }
                }
            }
            """);
}
