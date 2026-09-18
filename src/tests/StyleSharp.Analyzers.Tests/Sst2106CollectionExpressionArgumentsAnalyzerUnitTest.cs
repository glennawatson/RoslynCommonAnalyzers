// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using VerifyArguments = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2106CollectionExpressionArgumentsAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the collection-expression-arguments rule (SST2106).</summary>
public class Sst2106CollectionExpressionArgumentsAnalyzerUnitTest
{
    /// <summary>Verifies configuration is suggested only when a collection expression can preserve the initializer.</summary>
    /// <param name="source">A configured collection creation with expected diagnostic markup.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M() { Dictionary<string, int> values = new Dictionary<string, int>(4) { [\"a\"] = 1, [\"b\"] = 2 }; } }")]
    [Arguments("class C { void M() { Dictionary<string, int> values = new(StringComparer.Ordinal) { { \"a\", 1 }, { \"b\", 2 } }; } }")]
    [Arguments("class C { void M() { Dictionary<string, int> values = new(4, StringComparer.Ordinal) { [\"a\"] = 1, [\"a\"] = 2 }; } }")]
    [Arguments("class C { Dictionary<string, int> values = new(StringComparer.Ordinal) { [\"a\"] = 1, [\"b\"] = 2 }; }")]
    [Arguments("class C { Dictionary<string, int> values = new Dictionary<string, int>(4, StringComparer.Ordinal) { { \"a\", 1 }, { \"b\", 2 } }; }")]
    [Arguments("class C { Dictionary<string, int> values = new Dictionary<string, int>(4) { [\"a\"] = 1, [\"a\"] = 2 }; }")]
    [Arguments("class C { Dictionary<string, int> Values { get; } = new Dictionary<string, int>(4, StringComparer.Ordinal) { [\"a\"] = 1, [\"b\"] = 2 }; }")]
    [Arguments("class C { Dictionary<string, int> Values { get; } = new(4) { { \"a\", 1 }, { \"b\", 2 } }; }")]
    [Arguments("class C { Dictionary<string, int> Values { get; } = new(StringComparer.Ordinal) { [\"a\"] = 1, [\"a\"] = 2 }; }")]
    [Arguments("class C { Dictionary<string, int> M() => new(4) { [\"a\"] = 1, [\"b\"] = 2 }; }")]
    [Arguments("class C { Dictionary<string, int> M() => new(StringComparer.Ordinal) { { \"a\", 1 }, { \"b\", 2 } }; }")]
    [Arguments("class C { Dictionary<string, int> M() => new(4, StringComparer.Ordinal) { [\"a\"] = 1, [\"a\"] = 2 }; }")]
    [Arguments("class C { Dictionary<string, int> Values { get; } = {|SST2106:new(4, StringComparer.Ordinal) { }|}; }")]
    [Arguments("class C { HashSet<string> Values { get; } = {|SST2106:new(StringComparer.Ordinal) { }|}; }")]
    [Arguments("class C { List<int> Values { get; } = {|SST2106:new(4) { }|}; }")]
    [Arguments("class C { List<int> Values { get; } = {|SST2106:new(4) { 1, 2 }|}; }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InitializerConversionIsCheckedAsync(string source) =>
        RunAsync($"using System; using System.Collections.Generic; {source}");

    /// <summary>Verifies configuration is not moved into a collection expression with a different target type.</summary>
    /// <param name="source">A collection creation converted to an interface or object target.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("""
        using System; using System.Collections.Generic;
        class C { public IDictionary<string, object> Values { get; } = new Dictionary<string, object>(StringComparer.Ordinal); }
        """)]
    [Arguments("""
        using System; using System.Collections.Generic;
        class C { public IDictionary<string, object> Values = new Dictionary<string, object>(StringComparer.Ordinal); }
        """)]
    [Arguments("""
        using System; using System.Collections.Generic;
        class C
        {
            public IDictionary<string, object> M()
            {
                IDictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
                return values;
            }
        }
        """)]
    [Arguments("using System.Collections.Generic; class C { public IList<int> Values { get; } = new List<int>(4); }")]
    [Arguments("using System; using System.Collections.Generic; class C { public ISet<string> Values { get; } = new HashSet<string>(StringComparer.Ordinal); }")]
    [Arguments("using System.Collections.Generic; class C { public object Values { get; } = new List<int>(4); }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConvertedCollectionTargetIsCleanAsync(string source) => RunAsync(source);

    /// <summary>Verifies capacity and comparer arguments are reported for a concrete dictionary property.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConcreteDictionaryPropertyIsReportedAsync() =>
        RunAsync(
            """
            using System; using System.Collections.Generic;
            class C
            {
                public Dictionary<string, object> Values { get; } = {|SST2106:new Dictionary<string, object>(4, StringComparer.Ordinal)|};
            }
            """);

    /// <summary>Verifies named or by-reference arguments and missing constructors are ignored.</summary>
    /// <param name="source">The noncandidate collection creation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { System.Collections.Generic.List<int> items = new System.Collections.Generic.List<int>(capacity: 4); }")]
    [Arguments("class C { void M(int size) { System.Collections.Generic.List<int> items = new System.Collections.Generic.List<int>(ref size); } }")]
    [Arguments("class C { System.Collections.Generic.List<int> items = new System.Collections.Generic.List<int>(true); }")]
    [Arguments("class C { System.Collections.Generic.List<int> M() => new System.Collections.Generic.List<int>(4); }")]
    [Arguments("class C { void M(System.Collections.Generic.List<int> items = new System.Collections.Generic.List<int>(4)) { } }")]
    public async Task UnsupportedCreationContextIsCleanAsync(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new(LanguageVersion.Preview));
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform, new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Sst2106CollectionExpressionArgumentsAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a property initializer supplies an explicit collection target.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PropertyInitializerIsReportedAsync() =>
        RunAsync("using System.Collections.Generic; class C { List<int> Items { get; } = {|SST2106:new List<int>(4)|}; }");

    /// <summary>Verifies missing collection framework types disable configuration rewrites.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingCollectionFrameworkIsCleanAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class Items { public Items(int capacity) { } } class C { Items values = new Items(4); }", new(LanguageVersion.Preview));
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], options: new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Sst2106CollectionExpressionArgumentsAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a comparer argument with an element initializer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ComparerWithInitializerReportedAsync() =>
        RunAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CapacityReportedAsync() =>
        RunAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TargetTypedComparerReportedAsync() =>
        RunAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SeedFromSourceIsCleanAsync() =>
        RunAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task VarTargetIsCleanAsync() =>
        RunAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NoArgumentsIsCleanAsync() =>
        RunAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnsupportedCollectionIsCleanAsync() =>
        RunAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BelowCSharp15IsCleanAsync() =>
        RunAsync(
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
        var test = new VerifyArguments.Test { TestCode = source };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(languageVersion));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
