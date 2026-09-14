// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1006ConcurrentDictionaryClosureCaptureAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests factory syntax, concurrent dictionary receivers, and captured key identity.</summary>
public class ConcurrentDictionaryClosureCaptureAnalyzerUnitTest
{
    /// <summary>Verifies simple, parenthesized, block, and nested lambda bodies capture the key parameter.</summary>
    /// <param name="lambda">The factory that captures the outer key.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("k => key")]
    [Arguments("(k) => key")]
    [Arguments("(string k) => key")]
    [Arguments("k => key.ToUpperInvariant()")]
    [Arguments("k => { return key; }")]
    [Arguments("k => { var copy = k; return key + copy; }")]
    [Arguments("k => { System.Func<string> nested = () => key; return nested(); }")]
    public async Task CapturedParameterIsReportedAsync(string lambda)
    {
        var source = $$"""
            using System.Collections.Concurrent;
            public class C
            {
                public string M(ConcurrentDictionary<string, string> map, string key)
                    => map.GetOrAdd(key, {|PSH1006:{{lambda}}|});
            }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies key locals, escaped identifiers, and derived dictionary receivers are supported.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CapturedLocalOnDerivedDictionaryIsReportedAsync()
    {
        const string Source = """
            using System.Collections.Concurrent;
            public class Derived : ConcurrentDictionary<string, string> { }
            public class C
            {
                public string M(Derived map)
                {
                    string @key = "key";
                    return map.GetOrAdd(@key, {|PSH1006:k => @key|});
                }
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies each AddOrUpdate lambda is independently checked, including a value add argument.</summary>
    /// <param name="arguments">The marked arguments following the dictionary receiver.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("key, {|PSH1006:k => key|}, {|PSH1006:(k, old) => key + old|}")]
    [Arguments("key, k => k, {|PSH1006:(k, old) => key|}")]
    [Arguments("key, {|PSH1006:k => key|}, (k, old) => k + old")]
    [Arguments("key, key, {|PSH1006:(k, old) => key|}")]
    public async Task CapturingAddOrUpdateFactoriesAreReportedAsync(string arguments)
    {
        var source = $$"""
            using System.Collections.Concurrent;
            public class C
            {
                public string M(ConcurrentDictionary<string, string> map, string key)
                    => map.AddOrUpdate({{arguments}});
            }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies generic state overloads still diagnose the captured key.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CapturingStateOverloadsAreReportedAsync()
    {
        const string Source = """
            using System.Collections.Concurrent;
            public class C
            {
                public string M(ConcurrentDictionary<string, string> map, string key, string state)
                {
                    _ = map.GetOrAdd(key, {|PSH1006:(k, value) => key + value|}, state);
                    return map.AddOrUpdate(key, {|PSH1006:(k, value) => key + value|},
                        {|PSH1006:(k, old, value) => key + old + value|}, state);
                }
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies using the factory parameter, unrelated captures, constants, and shadowing fields are clean.</summary>
    /// <param name="lambda">The factory without a reference to the bound key variable.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("k => k")]
    [Arguments("(k) => k")]
    [Arguments("static k => k")]
    [Arguments("k => other")]
    [Arguments("k => \"constant\"")]
    [Arguments("k => k.ToUpperInvariant()")]
    [Arguments("k => { return k; }")]
    [Arguments("k => this.key")]
    [Arguments("k => holder.key")]
    public async Task FactoryWithoutKeyCaptureIsCleanAsync(string lambda)
    {
        var source = $$"""
            using System.Collections.Concurrent;
            public class C
            {
                public string key = "field";
                public string M(ConcurrentDictionary<string, string> map, string key, string other, C holder)
                    => map.GetOrAdd(key, {{lambda}});
            }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies fields and properties used as the key are not mistaken for captured locals.</summary>
    /// <param name="keyMember">The non-local key declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("private string key = \"field\";")]
    [Arguments("private string key => \"property\";")]
    [Arguments("private static string key = \"static\";")]
    public async Task NonLocalKeyIsCleanAsync(string keyMember)
    {
        var source = $$"""
            using System.Collections.Concurrent;
            public class C
            {
                {{keyMember}}
                public string M(ConcurrentDictionary<string, string> map) => map.GetOrAdd(key, k => key);
            }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies nullable values and a generic dictionary key keep their symbol identity.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GenericKeyAndNullableValueAreReportedAsync()
    {
        const string Source = """
            #nullable enable
            using System.Collections.Concurrent;
            public class C
            {
                public T? M<T>(ConcurrentDictionary<T, T?> map, T key) where T : notnull
                    => map.GetOrAdd(key, {|PSH1006:k => key|});
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies named keys, expressions, method groups, anonymous methods, and other names are not candidates.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FactorySyntaxNearMissesAreCleanAsync()
    {
        const string Source = """
            using System.Collections.Concurrent;
            public class C
            {
                public string M(ConcurrentDictionary<string, string> map, string key)
                {
                    _ = map.GetOrAdd(key: key, valueFactory: k => key);
                    _ = map.GetOrAdd((key), k => key);
                    _ = map.GetOrAdd(key + "", k => key);
                    _ = map.GetOrAdd("literal", k => key);
                    _ = map.GetOrAdd(key, Factory);
                    _ = map.GetOrAdd(key, delegate(string k) { return key; });
                    _ = map.GetOrAdd(key, key);
                    _ = map.TryAdd(key, key);
                    _ = map?.GetOrAdd(key, k => key);
                    return key;
                }
                private static string Factory(string key) => key;
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies matching foreign methods and unknown receiver types cannot pass the dictionary gate.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForeignReceiverIsCleanAsync()
    {
        const string Source = """
            using System;
            public class Other
            {
                public string GetOrAdd(string key, Func<string, string> factory) => key;
            }
            public class C
            {
                public string M(Other map, string key) => map.GetOrAdd(key, k => key);
                public string N(string key) => {|CS0103:missing|}.GetOrAdd(key, k => key);
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an unresolved key symbol does not produce a capture diagnostic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnresolvedKeyIsCleanAsync()
    {
        const string Source = """
            using System.Collections.Concurrent;
            public class C
            {
                public string M(ConcurrentDictionary<string, string> map)
                    => map.GetOrAdd({|CS0103:missing|}, k => {|CS0103:missing|});
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies zero-parameter factories are skipped even when another factory makes the call a candidate.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParameterlessFactoryIsCleanAsync()
    {
        const string Source = """
            using System;
            using System.Collections.Concurrent;
            public class Map : ConcurrentDictionary<string, string>
            {
                public string GetOrAdd(string key, Func<string> factory) => key;
                public string AddOrUpdate(string key, Func<string> add, Func<string, string> update) => key;
            }
            public class C
            {
                public string M(Map map, string key)
                {
                    _ = map.GetOrAdd(key, () => key);
                    return map.AddOrUpdate(key, () => key, {|PSH1006:k => key|});
                }
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies all syntax filters, including malformed pointer calls and ref-kind arguments.</summary>
    /// <param name="expression">The candidate invocation.</param>
    /// <param name="matches">Whether the syntax gate should accept it.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("map.GetOrAdd(key, k => key)", true)]
    [Arguments("map.AddOrUpdate(key, value, (k, old) => key)", true)]
    [Arguments("map.GetOrAdd(key, (k) => key)", true)]
    [Arguments("GetOrAdd(key, k => key)", false)]
    [Arguments("map->GetOrAdd(key, k => key)", false)]
    [Arguments("map.Other(key, k => key)", false)]
    [Arguments("map.GetOrAdd()", false)]
    [Arguments("map.GetOrAdd(key)", false)]
    [Arguments("map.GetOrAdd(key: key, k => key)", false)]
    [Arguments("map.GetOrAdd(ref key, k => key)", false)]
    [Arguments("map.GetOrAdd(in key, k => key)", false)]
    [Arguments("map.GetOrAdd(out key, k => key)", false)]
    [Arguments("map.GetOrAdd(this.key, k => key)", false)]
    [Arguments("map.GetOrAdd(key, value)", false)]
    [Arguments("map.GetOrAdd(key, () => key)", false)]
    [Arguments("map.GetOrAdd(key, delegate { return key; })", false)]
    public async Task FactoryShapeIsClassifiedAsync(string expression, bool matches)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(expression);
        var actual = Psh1006ConcurrentDictionaryClosureCaptureAnalyzer.TryGetFactoryCallShape(invocation, out var member, out var key);
        await Assert.That(actual).IsEqualTo(matches);
        await Assert.That(member is not null).IsEqualTo(matches);
        await Assert.That(key?.Identifier.ValueText).IsEqualTo(matches ? "key" : null);
    }

    /// <summary>Verifies a same-spelled field is distinct from the parameter and unresolved names are not captures.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CapturedReferenceRequiresSymbolIdentityAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            public class C
            {
                public string key;
                public string M(string key) => key + this.key + missing;
            }
            """);
        var compilation = CSharpCompilation.Create("KeyReference", [tree], [RuntimeMetadataReferences.CoreLibrary]);
        var model = compilation.GetSemanticModel(tree);
        var root = await tree.GetRootAsync();
        var parameter = root.DescendantNodes().OfType<ParameterSyntax>().Single();
        var symbol = model.GetDeclaredSymbol(parameter)!;
        var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>().ToArray();
        await Assert.That(Psh1006ConcurrentDictionaryClosureCaptureAnalyzer.IsCapturedKeyReference(model, identifiers[0], "key", symbol, CancellationToken.None)).IsTrue();
        await Assert.That(Psh1006ConcurrentDictionaryClosureCaptureAnalyzer.IsCapturedKeyReference(model, identifiers[1], "key", symbol, CancellationToken.None)).IsFalse();
        await Assert.That(Psh1006ConcurrentDictionaryClosureCaptureAnalyzer.IsCapturedKeyReference(model, identifiers[2], "missing", symbol, CancellationToken.None)).IsFalse();
        await Assert.That(Psh1006ConcurrentDictionaryClosureCaptureAnalyzer.IsCapturedKeyReference(model, identifiers[0], "other", symbol, CancellationToken.None)).IsFalse();
    }

    /// <summary>Verifies missing concurrent dictionary metadata disables otherwise matching factory calls.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingConcurrentDictionaryFrameworkIsCleanAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = """
                using System;
                public class Other
                {
                    public string GetOrAdd(string key, Func<string, string> factory) => key;
                }
                public class C
                {
                    public string M(Other map, string key) => map.GetOrAdd(key, k => key);
                }
                """,
        };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectMetadataReferences(projectId, [RuntimeMetadataReferences.CoreLibrary]));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a marked-source test against the cached .NET 9 reference set.</summary>
    /// <param name="source">The consumer source and expected diagnostics.</param>
    /// <returns>A task representing the asynchronous verification.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source };
        await test.RunAsync(CancellationToken.None);
    }
}
