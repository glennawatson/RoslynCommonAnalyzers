// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1406UseDirectRegexQueriesAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests direct regex queries and their framework and binding requirements.</summary>
public class UseDirectRegexQueriesAnalyzerUnitTest
{
    /// <summary>The cached reference set for compilations with minimal framework stubs.</summary>
    private static readonly ImmutableArray<MetadataReference> CoreReferences = [RuntimeMetadataReferences.CoreLibrary];

    /// <summary>Verifies static, instance, and imported regex chains report their trailing property.</summary>
    /// <param name="expression">The marked regex query.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("regex.Match(input).{|PSH1406:Success|}")]
    [Arguments("Regex.Match(input, \"x\").{|PSH1406:Success|}")]
    [Arguments("Match(input, \"x\").{|PSH1406:Success|}")]
    [Arguments("regex.Matches(input).{|PSH1406:Count|}")]
    [Arguments("Regex.Matches(input, \"x\").{|PSH1406:Count|}")]
    [Arguments("Matches(input, \"x\").{|PSH1406:Count|}")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DirectRegexQueryIsReportedAsync(string expression) =>
        VerifyAsync(
            $$"""
            using System.Text.RegularExpressions;
            using static System.Text.RegularExpressions.Regex;
            public class C
            {
                public object M(Regex regex, string input) => {{expression}};
            }
            """,
            AnalyzerFrameworks.Net90);

    /// <summary>Verifies materialized locals, other properties, and parenthesized delegates remain unchanged.</summary>
    /// <param name="expression">The near-miss query.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("match.Success")]
    [Arguments("matches.Count")]
    [Arguments("regex.Match(input).Length")]
    [Arguments("regex.Matches(input).GetEnumerator()")]
    [Arguments("Other(input).Success")]
    [Arguments("OtherMatches(input).Count")]
    [Arguments("((find))(input).Success")]
    [Arguments("((findAll))(input).Count")]
    [Arguments("(regex.Match(input)).Success")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonDirectQueryIsCleanAsync(string expression) =>
        VerifyAsync(
            $$"""
            using System;
            using System.Text.RegularExpressions;
            public class C
            {
                public object M(Regex regex, string input, Match match, MatchCollection matches,
                    Func<string, Match> find, Func<string, MatchCollection> findAll) => {{expression}};
                private static Match Other(string input) => Regex.Match(input, "x");
                private static MatchCollection OtherMatches(string input) => Regex.Matches(input, "x");
            }
            """,
            AnalyzerFrameworks.Net90);

    /// <summary>Verifies user methods with the same names do not bind to Regex.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MatchingNamesOnOtherTypesAreCleanAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;
            public class C
            {
                public bool M(string input) => Match(input).Success;
                public int N(string input) => Matches(input).Count;
                private static Match Match(string input) => Regex.Match(input, "x");
                private static MatchCollection Matches(string input) => Regex.Matches(input, "x");
            }
            """,
            AnalyzerFrameworks.Net90);

    /// <summary>Verifies an unbound regex overload does not produce a recommendation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnresolvedRegexCallIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;
            public class C
            {
                public bool M() => Regex.{|CS1501:Match|}().Success;
            }
            """,
            AnalyzerFrameworks.Net90);

    /// <summary>Verifies Match.Success is supported even on frameworks without Regex.Count.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LegacyFrameworkReportsOnlyBooleanQueryAsync() =>
        VerifyAsync(
            """
            using System.Text.RegularExpressions;
            public class C
            {
                public bool M(Regex regex, string input) => regex.Match(input).{|PSH1406:Success|};
                public int N(Regex regex, string input) => regex.Matches(input).Count;
            }
            """,
            AnalyzerFrameworks.NetStandard20);

    /// <summary>Verifies a compilation without Regex can still contain similarly named calls.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MissingRegexTypeIsCleanAsync() =>
        VerifyCoreLibraryAsync(
            """
            public class C
            {
                public bool Success => true;
                public int Count => 1;
                public C Match() => this;
                public C Matches() => this;
                public bool M() => Match().Success;
                public int N() => Matches().Count;
            }
            """);

    /// <summary>Verifies a Count property is insufficient to enable the direct count suggestion.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CountPropertyDoesNotSatisfyMethodRequirementAsync() =>
        VerifyCoreLibraryAsync(
            """
            namespace System.Text.RegularExpressions
            {
                public class Regex
                {
                    public int Count => 0;
                    public Regex Matches(string input) => this;
                }
            }
            public class C
            {
                public int M(System.Text.RegularExpressions.Regex regex) => regex.Matches("x").Count;
            }
            """);

    /// <summary>Verifies a missing member access returns no invocation or replacement name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingQueryShapeHasNoReplacementAsync()
    {
        var matches = Psh1406UseDirectRegexQueriesAnalyzer.TryGetQueryShape(null!, out var invocation, out var replacementName);

        await Assert.That(matches).IsFalse();
        await Assert.That(invocation).IsNull();
        await Assert.That(replacementName).IsEmpty();
    }

    /// <summary>Runs analyzer verification against the requested cached framework references.</summary>
    /// <param name="source">The source to analyze.</param>
    /// <param name="framework">The target framework's cached references.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task VerifyAsync(string source, ReferenceAssemblies framework) =>
        new Verify.Test { TestCode = source, ReferenceAssemblies = framework }.RunAsync(CancellationToken.None);

    /// <summary>Runs analyzer verification with only the cached core library reference.</summary>
    /// <param name="source">The source, including any minimal framework stubs.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static Task VerifyCoreLibraryAsync(string source)
    {
        var test = new Verify.Test { TestCode = source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
            solution.WithProjectMetadataReferences(projectId, CoreReferences));
        return test.RunAsync(CancellationToken.None);
    }
}
