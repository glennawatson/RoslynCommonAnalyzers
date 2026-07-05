// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyConditionalDelegateInvocation = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2240ConditionalDelegateInvocationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2240ConditionalDelegateInvocationAnalyzer"/>.</summary>
public class ConditionalDelegateInvocationAnalyzerUnitTest
{
    /// <summary>Verifies a null-checked delegate that is immediately invoked is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullCheckedDelegateInvocationIsReportedAsync()
        => await RunAsync(
            """
            using System;

            public sealed class C
            {
                private Action _changed = null!;

                public void M()
                {
                    {|SST2240:if|} (_changed != null)
                    {
                        _changed();
                    }
                }
            }
            """);

    /// <summary>Verifies a null check that does not invoke the checked delegate is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonInvocationGuardIsCleanAsync()
        => await RunAsync(
            """
            using System;

            public sealed class C
            {
                public void M(Action callback)
                {
                    if (callback != null)
                    {
                        Console.WriteLine(1);
                    }
                }
            }
            """);

    /// <summary>Runs the analyzer verifier with modern reference assemblies.</summary>
    /// <param name="source">The source code to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunAsync(string source)
        => await new VerifyConditionalDelegateInvocation.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source
        }.RunAsync(CancellationToken.None);
}
