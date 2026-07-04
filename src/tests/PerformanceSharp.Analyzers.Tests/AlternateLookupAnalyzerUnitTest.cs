// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1116AlternateLookupAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1116AlternateLookupAnalyzer"/> (PSH1116 alternate lookups).</summary>
public class AlternateLookupAnalyzerUnitTest
{
    /// <summary>Verifies a span materialized with ToString for a dictionary probe is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpanToStringProbeIsReportedAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public bool M(Dictionary<string, int> map, ReadOnlySpan<char> name)
                    => map.ContainsKey({|PSH1116:name.ToString()|});
            }
            """);

    /// <summary>Verifies a span materialized with the string constructor for a set probe is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewStringSetProbeIsReportedAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public bool M(HashSet<string> names, Span<char> name)
                    => names.Contains({|PSH1116:new string(name)|});
            }
            """);

    /// <summary>Verifies probing with an existing string stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringKeyProbeIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public bool M(Dictionary<string, int> map, string name) => map.ContainsKey(name);
            }
            """);

    /// <summary>Verifies a materialized key that outlives the probe stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MaterializedKeyStoredFirstIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public string M(Dictionary<string, int> map, ReadOnlySpan<char> name)
                {
                    var key = name.ToString();
                    map.ContainsKey(key);
                    return key;
                }
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
