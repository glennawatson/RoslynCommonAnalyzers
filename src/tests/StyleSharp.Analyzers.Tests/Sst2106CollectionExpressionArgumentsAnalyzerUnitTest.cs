// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using VerifyArguments = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2106CollectionExpressionArgumentsAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the collection-expression-arguments rule (SST2106).</summary>
public class Sst2106CollectionExpressionArgumentsAnalyzerUnitTest
{
    /// <summary>Verifies a comparer argument with an element initializer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparerWithInitializerReportedAsync()
        => await RunAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                private static readonly HashSet<string> Names = {|SST2106:new HashSet<string>(StringComparer.Ordinal) { "a" }|};
            }
            """);

    /// <summary>Verifies a capacity argument on an explicitly typed local is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapacityReportedAsync()
        => await RunAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M()
                {
                    List<int> values = {|SST2106:new List<int>(4)|};
                    values.Add(1);
                }
            }
            """);

    /// <summary>Verifies a target-typed creation carrying a comparer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TargetTypedComparerReportedAsync()
        => await RunAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                private readonly Dictionary<string, int> _totals = {|SST2106:new(StringComparer.Ordinal)|};

                public int Count => _totals.Count;
            }
            """);

    /// <summary>Verifies seeding from a source is not reported, because that rewrite is a spread element.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeedFromSourceIsCleanAsync()
        => await RunAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public List<int> M(IEnumerable<int> source)
                {
                    List<int> values = new List<int>(source);
                    return values;
                }
            }
            """);

    /// <summary>Verifies a 'var' target is not reported, because a collection expression needs a written target type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VarTargetIsCleanAsync()
        => await RunAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M()
                {
                    var values = new List<int>(4);
                    values.Add(1);
                }
            }
            """);

    /// <summary>Verifies a parameterless creation is not reported by this rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoArgumentsIsCleanAsync()
        => await RunAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M()
                {
                    List<int> values = new List<int>();
                    values.Add(1);
                }
            }
            """);

    /// <summary>Verifies an unsupported collection type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsupportedCollectionIsCleanAsync()
        => await RunAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M()
                {
                    Queue<int> values = new Queue<int>(4);
                    values.Enqueue(1);
                }
            }
            """);

    /// <summary>Verifies nothing is reported below C# 15, where 'with(...)' does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BelowCSharp15IsCleanAsync()
        => await RunAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal) { "a" };
            }
            """,
            LanguageVersion.CSharp13);

    /// <summary>Runs the analyzer verifier at the requested language version.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="languageVersion">The language version to parse with.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var test = new VerifyArguments.Test
        {
            TestCode = source
        };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(languageVersion));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
