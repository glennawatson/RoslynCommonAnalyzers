// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1011UseStateOverloadAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1011UseStateOverloadAnalyzer"/> (PSH1011 state overloads).</summary>
public class UseStateOverloadAnalyzerUnitTest
{
    /// <summary>Verifies a capturing ContinueWith lambda is reported; the state overload exists.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapturingContinueWithLambdaIsReportedAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public void M(Task task, string label)
                    => task.ContinueWith({|PSH1011:t => Console.WriteLine(label)|});
            }
            """);

    /// <summary>Verifies a capturing UnsafeRegister callback is reported; the data fits the state argument.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapturingRegisterCallbackIsReportedAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Threading;

            public class C
            {
                public void M(CancellationToken token, IDisposable resource)
                    => token.Register({|PSH1011:() => resource.Dispose()|});
            }
            """);

    /// <summary>Verifies a capture-free lambda stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CaptureFreeLambdaIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public void M(Task task)
                    => task.ContinueWith(static t => Console.WriteLine(t.Status));
            }
            """);

    /// <summary>Verifies a capturing lambda passed to an API with no state overload stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoStateOverloadIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public void M(List<int> values, int threshold)
                    => values.RemoveAll(value => value > threshold);
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        await test.RunAsync(CancellationToken.None);
    }
}
