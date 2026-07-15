// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2321LibraryProcessTerminationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2321 (library code terminating the host process).</summary>
public class Sst2321LibraryProcessTerminationAnalyzerUnitTest
{
    /// <summary>Verifies <c>Environment.Exit</c> in a library is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnvironmentExitInLibraryIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void Shutdown()
                {
                    {|SST2321:Environment.Exit(0)|};
                }
            }
            """);

    /// <summary>Verifies <c>Environment.FailFast</c> in a library is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnvironmentFailFastInLibraryIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void Panic()
                {
                    {|SST2321:Environment.FailFast("unrecoverable")|};
                }
            }
            """);

    /// <summary>Verifies a fully qualified terminating call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedTerminationIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Shutdown()
                {
                    {|SST2321:System.Environment.Exit(1)|};
                }
            }
            """);

    /// <summary>Verifies the same calls are not reported when the compilation is an executable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>An application owns its process, so ending it is a legitimate choice the rule leaves alone.</remarks>
    [Test]
    public async Task TerminationInExecutableIsCleanAsync()
    {
        var test = new Verify.Test
        {
            TestState =
            {
                OutputKind = OutputKind.ConsoleApplication,
                Sources =
                {
                    ("Program.cs", """
                        using System;

                        public static class Program
                        {
                            public static void Main()
                            {
                                Environment.Exit(0);
                                Environment.FailFast("done");
                            }
                        }
                        """),
                },
            },
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an <c>Exit</c> method on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExitOnUnrelatedTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public static class Gate
            {
                public static void Exit(int code)
                {
                }

                public static void FailFast(string message)
                {
                }
            }

            public sealed class C
            {
                public void Close()
                {
                    Gate.Exit(0);
                    Gate.FailFast("closing");
                }
            }
            """);
}
