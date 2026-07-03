// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1009UnboundedStackallocAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1009UnboundedStackallocAnalyzer"/> (PSH1009 unbounded stackalloc).</summary>
public class UnboundedStackallocAnalyzerUnitTest
{
    /// <summary>Verifies a data-driven length with no bound is flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnboundedLengthIsFlaggedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public int M(int length)
                {
                    Span<byte> buffer = {|PSH1009:stackalloc byte[length]|};
                    return buffer.Length;
                }
            }
            """);

    /// <summary>Verifies a constant length is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantLengthIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public int M()
                {
                    Span<byte> buffer = stackalloc byte[256];
                    return buffer.Length;
                }
            }
            """);

    /// <summary>Verifies the guarded conditional spill shape is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedConditionalIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public int M(int length)
                {
                    Span<char> buffer = length <= 512 ? stackalloc char[length] : new char[length];
                    return buffer.Length;
                }
            }
            """);

    /// <summary>Verifies an enclosing if guard with a constant comparison is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnclosingIfGuardIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public int M(int length)
                {
                    if (length <= 512)
                    {
                        Span<byte> buffer = stackalloc byte[length];
                        return buffer.Length;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a relational pattern guard is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RelationalPatternGuardIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public int M(int length)
                {
                    if (length is > 0 and <= 512)
                    {
                        Span<byte> buffer = stackalloc byte[length];
                        return buffer.Length;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies a Math.Min clamped length is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MinClampedLengthIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public int M(int length)
                {
                    Span<char> buffer = stackalloc char[Math.Min(length, 64)];
                    return buffer.Length;
                }
            }
            """);

    /// <summary>Verifies a static readonly threshold length is treated as bounded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticReadonlyLengthIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                private static readonly int Threshold = 512;

                public int M()
                {
                    Span<byte> buffer = stackalloc byte[Threshold];
                    return buffer.Length;
                }
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        await test.RunAsync(CancellationToken.None);
    }
}
