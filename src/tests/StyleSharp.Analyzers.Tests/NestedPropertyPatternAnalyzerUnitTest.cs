// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyNestedPropertyPattern = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2238NestedPropertyPatternAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2238NestedPropertyPatternAnalyzer"/>.</summary>
public class NestedPropertyPatternAnalyzerUnitTest
{
    /// <summary>Verifies a nested property-only pattern is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NestedPropertyPatternIsReportedAsync()
        => await RunAsync(
            """
            public sealed class Person
            {
                public Address Address { get; set; } = new();
            }

            public sealed class Address
            {
                public string City { get; set; } = "";
            }

            public sealed class C
            {
                public bool M(Person person) => person is { Address: {|SST2238:{ City: "Melbourne" }|} };
            }
            """);

    /// <summary>Verifies declaration patterns are clean because flattening would change the pattern shape.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DeclarationPatternIsCleanAsync()
        => await RunAsync(
            """
            public sealed class Person
            {
                public object Value { get; set; } = new();
            }

            public sealed class C
            {
                public bool M(Person person) => person is { Value: string text };
            }
            """);

    /// <summary>Runs the analyzer verifier with modern reference assemblies.</summary>
    /// <param name="source">The source code to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunAsync(string source)
        => await new VerifyNestedPropertyPattern.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source
        }.RunAsync(CancellationToken.None);
}
