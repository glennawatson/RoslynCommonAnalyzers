// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1213UseSearchValuesAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1213UseSearchValuesAnalyzer"/> (PSH1213 SearchValues).</summary>
public class UseSearchValuesAnalyzerUnitTest
{
    /// <summary>Verifies an inline array passed to IndexOfAny is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineArrayIndexOfAnyIsReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public int M(string value) => {|PSH1213:value.IndexOfAny(new[] { ';', ',', '|' })|};
            }
            """);

    /// <summary>Verifies a collection expression passed to a span search is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollectionExpressionSpanSearchIsReportedAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public bool M(ReadOnlySpan<char> text) => {|PSH1213:text.ContainsAny([';', ','])|};
            }
            """);

    /// <summary>Verifies a hoisted field argument stays clean; only inline creations are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldArgumentIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                private static readonly char[] Separators = { ';', ',' };

                public int M(string value) => value.IndexOfAny(Separators);
            }
            """);

    /// <summary>Verifies a non-constant inline set stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantSetIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public int M(string value, char extra) => value.IndexOfAny(new[] { ';', extra });
            }
            """);

    /// <summary>Verifies a framework without SearchValues stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FrameworkWithoutSearchValuesIsCleanAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            TestCode = """
                       public class C
                       {
                           public int M(string value) => value.IndexOfAny(new[] { ';', ',', '|' });
                       }
                       """,
        };
        await test.RunAsync(CancellationToken.None);
    }

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
