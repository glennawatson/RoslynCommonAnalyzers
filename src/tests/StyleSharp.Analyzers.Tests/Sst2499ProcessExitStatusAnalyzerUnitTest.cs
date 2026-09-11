// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using VerifyExitStatus = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2499ProcessExitStatusAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the process-exit-status rule (SST2499).</summary>
public class Sst2499ProcessExitStatusAnalyzerUnitTest
{
    /// <summary>Verifies an exit-code read beside a wait call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExitCodeAfterWaitReportedAsync() =>
        RunAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public int Run(Process process)
                {
                    process.WaitForExit();
                    return {|SST2499:process.ExitCode|};
                }
            }
            """);

    /// <summary>Verifies an exit-code read beside an asynchronous wait call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExitCodeAfterAsyncWaitReportedAsync() =>
        RunAsync(
            """
            using System.Diagnostics;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<int> RunAsync(Process process)
                {
                    await process.WaitForExitAsync();
                    return {|SST2499:process.ExitCode|};
                }
            }
            """);

    /// <summary>Verifies an exit-code read with no wait in the same member is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExitCodeWithoutWaitIsCleanAsync() =>
        RunAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public int Read(Process process) => process.ExitCode;
            }
            """);

    /// <summary>Verifies an unrelated ExitCode member is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedExitCodeIsCleanAsync() =>
        RunAsync(
            """
            public class Job
            {
                public int ExitCode { get; set; }

                public void WaitForExit()
                {
                }
            }

            public class C
            {
                public int Run(Job job)
                {
                    job.WaitForExit();
                    return job.ExitCode;
                }
            }
            """);

    /// <summary>Verifies nothing is reported on a framework without the replacement API.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WithoutReplacementApiIsCleanAsync() =>
        RunAsync(
            """
            using System.Diagnostics;

            public class C
            {
                public int Run(Process process)
                {
                    process.WaitForExit();
                    return process.ExitCode;
                }
            }
            """,
            ReferenceAssemblies.Net.Net80);

    /// <summary>Runs the analyzer verifier against the requested reference assemblies.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="referenceAssemblies">The framework to compile the test source against.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, ReferenceAssemblies? referenceAssemblies = null)
    {
        var test = new VerifyExitStatus.Test { ReferenceAssemblies = referenceAssemblies ?? DotNet11ReferenceAssemblies.Net110, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }
}
