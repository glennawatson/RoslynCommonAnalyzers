// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCatchNullReference = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2401CatchNullReferenceAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2401 (a null-dereference failure caught rather than prevented).</summary>
public class CatchNullReferenceAnalyzerUnitTest
{
    /// <summary>Verifies a catch clause naming the type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CatchClauseIsReportedAsync()
        => await VerifyCatchNullReference.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class C
            {
                public void M(string? value)
                {
                    try
                    {
                        Console.WriteLine(value!.Length);
                    }
                    catch ({|SST2401:NullReferenceException|})
                    {
                        Console.WriteLine("oops");
                    }
                }
            }
            """);

    /// <summary>Verifies a fully qualified catch clause is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedCatchClauseIsReportedAsync()
        => await VerifyCatchNullReference.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class C
            {
                public void M(string? value)
                {
                    try
                    {
                        Console.WriteLine(value!.Length);
                    }
                    catch ({|SST2401:System.NullReferenceException|} error)
                    {
                        Console.WriteLine(error.Message);
                    }
                }
            }
            """);

    /// <summary>Verifies a filter that reaches the type is reported, not just a clause that names it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExceptionFilterIsReportedAsync()
        => await VerifyCatchNullReference.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class C
            {
                public void M(string? value)
                {
                    try
                    {
                        Console.WriteLine(value!.Length);
                    }
                    catch (Exception error) when (error is {|SST2401:NullReferenceException|})
                    {
                        Console.WriteLine(error.Message);
                    }
                }
            }
            """);

    /// <summary>Verifies a specific exception type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpecificExceptionIsCleanAsync()
        => await VerifyCatchNullReference.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(string value)
                {
                    try
                    {
                        Console.WriteLine(value.Length);
                    }
                    catch (InvalidOperationException error)
                    {
                        Console.WriteLine(error.Message);
                    }
                }
            }
            """);

    /// <summary>Verifies a type of the project's own with the same name is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedProjectTypeIsCleanAsync()
        => await VerifyCatchNullReference.VerifyAnalyzerAsync(
            """
            namespace Custom
            {
                public sealed class NullReferenceException : System.Exception
                {
                }

                public sealed class C
                {
                    public void M(string value)
                    {
                        try
                        {
                            System.Console.WriteLine(value.Length);
                        }
                        catch (NullReferenceException error)
                        {
                            System.Console.WriteLine(error.Message);
                        }
                    }
                }
            }
            """);
}
