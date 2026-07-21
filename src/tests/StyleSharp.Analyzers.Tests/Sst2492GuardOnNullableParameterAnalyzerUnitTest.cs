// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;
using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2492GuardOnNullableParameterAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2492 (a null guard applied to a parameter the signature allows to be null).</summary>
public class Sst2492GuardOnNullableParameterAnalyzerUnitTest
{
    /// <summary>Verifies a hand-written throw guard on a nullable-annotated parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowGuardOnAnnotatedParameterIsReportedAsync()
        => await VerifyAsync("""
            #nullable enable
            using System;

            public sealed class C
            {
                public void M(string? value)
                {
                    {|SST2492:if (value is null) throw new ArgumentNullException(nameof(value));|}
                }
            }
            """);

    /// <summary>Verifies a ThrowIfNull guard on a nullable-annotated parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowIfNullOnAnnotatedParameterIsReportedAsync()
        => await VerifyAsync("""
            #nullable enable
            using System;

            public sealed class C
            {
                public void M(string? value)
                {
                    {|SST2492:ArgumentNullException.ThrowIfNull(value)|};
                }
            }
            """);

    /// <summary>Verifies a throw guard on an optional parameter defaulting to null is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowGuardOnOptionalNullParameterIsReportedAsync()
        => await VerifyAsync("""
            using System;

            public sealed class C
            {
                public void M(string value = null)
                {
                    {|SST2492:if (value is null) throw new ArgumentNullException(nameof(value));|}
                }
            }
            """);

    /// <summary>Verifies a throw guard on a non-nullable reference parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowGuardOnNonNullableParameterIsCleanAsync()
        => await VerifyAsync("""
            #nullable enable
            using System;

            public sealed class C
            {
                public void M(string value)
                {
                    ArgumentNullException.ThrowIfNull(value);
                    if (value is null) throw new ArgumentNullException(nameof(value));
                }
            }
            """);

    /// <summary>Verifies a guard on a null-checked local rather than a parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardOnLocalIsCleanAsync()
        => await VerifyAsync("""
            #nullable enable
            using System;

            public sealed class C
            {
                public void M()
                {
                    string? value = Get();
                    ArgumentNullException.ThrowIfNull(value);
                    if (value is null) throw new ArgumentNullException(nameof(value));
                }

                private static string? Get() => null;
            }
            """);

    /// <summary>Runs an analyzer verification against the .NET 8 reference assemblies.</summary>
    /// <param name="source">The source with any diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
