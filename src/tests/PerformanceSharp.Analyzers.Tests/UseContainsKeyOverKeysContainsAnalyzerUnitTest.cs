// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1407UseContainsKeyOverKeysContainsAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests key-view membership, accessible replacements, and receiver type traversal.</summary>
public class UseContainsKeyOverKeysContainsAnalyzerUnitTest
{
    /// <summary>Verifies native and extension Contains calls on supported dictionary surfaces.</summary>
    /// <param name="receiverType">The static dictionary type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Dictionary<string, int>")]
    [Arguments("IDictionary<string, int>")]
    [Arguments("IReadOnlyDictionary<string, int>")]
    [Arguments("SortedDictionary<string, int>")]
    [Arguments("System.Collections.Concurrent.ConcurrentDictionary<string, int>")]
    [Arguments("Derived")]
    [Arguments("IInherited")]
    public async Task DictionaryKeysContainsIsReportedAsync(string receiverType)
    {
        var source = $$"""
            using System.Collections.Generic;
            using System.Linq;
            public class Derived : Dictionary<string, int> { }
            public interface IInherited : IDictionary<string, int> { }
            public class C
            {
                public bool M({{receiverType}} dictionary, string key) => dictionary.Keys.{|PSH1407:Contains|}(key);
            }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Records that the current rule does not traverse a type parameter's constraint types.</summary>
    /// <param name="constraint">The receiver constraint.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("IDictionary<string, int>")]
    [Arguments("IReadOnlyDictionary<string, int>")]
    [Arguments("Dictionary<string, int>")]
    [Arguments("IEmpty, IInherited")]
    public async Task ConstrainedDictionaryCurrentlyIsCleanAsync(string constraint)
    {
        var source = $$"""
            using System.Collections.Generic;
            using System.Linq;
            public interface IEmpty { }
            public interface IInherited : IDictionary<string, int> { }
            public class C
            {
                public bool M<T>(T dictionary, string key) where T : {{constraint}}
                    => dictionary.Keys.Contains(key);
            }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies nullable receivers and nullable generic values do not hide a valid replacement.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NullableDictionaryIsReportedAsync()
    {
        const string Source = """
            #nullable enable
            using System.Collections.Generic;
            using System.Linq;
            public class C
            {
                public bool M<T>(Dictionary<string, T?>? dictionary, string key)
                    => dictionary!.Keys.{|PSH1407:Contains|}(key);
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies custom key views qualify when their receiver declares the required public method.</summary>
    /// <param name="members">The replacement method surface.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public bool ContainsKey(string key) => false;")]
    [Arguments("public int ContainsKey() => 0; public bool ContainsKey(string key) => false;")]
    public async Task CustomDictionaryWithReplacementIsReportedAsync(string members)
    {
        var source = $$"""
            using System.Collections.Generic;
            public class Map
            {
                public List<string> Keys => null;
                {{members}}
            }
            public class C { public bool M(Map map) => map.Keys.{|PSH1407:Contains|}("key"); }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Records that the current rule checks replacement shape but not argument compatibility.</summary>
    /// <param name="member">A replacement shape that cannot actually accept the original call.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public bool ContainsKey(int key) => false;")]
    [Arguments("public bool ContainsKey<T>(string key) => false;")]
    [Arguments("public bool ContainsKey(ref string key) => false;")]
    public async Task IncompatibleReplacementCurrentlyReportsAsync(string member)
    {
        var source = $$"""
            using System.Collections.Generic;
            public class Map
            {
                public List<string> Keys => null;
                {{member}}
            }
            public class C { public bool M(Map map) => map.Keys.{|PSH1407:Contains|}("key"); }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies each failed replacement signature component closes the rule.</summary>
    /// <param name="member">The absent or unsuitable replacement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("public bool ContainsKey => false;")]
    [Arguments("public static bool ContainsKey(string key) => false;")]
    [Arguments("private bool ContainsKey(string key) => false;")]
    [Arguments("protected bool ContainsKey(string key) => false;")]
    [Arguments("internal bool ContainsKey(string key) => false;")]
    [Arguments("public bool ContainsKey() => false;")]
    [Arguments("public bool ContainsKey(string key, int other) => false;")]
    [Arguments("public int ContainsKey(string key) => 0;")]
    [Arguments("public bool? ContainsKey(string key) => false;")]
    public async Task UnsuitableReplacementIsCleanAsync(string member)
    {
        var source = $$"""
            using System.Collections.Generic;
            public class Map
            {
                public List<string> Keys => null;
                {{member}}
            }
            public class C { public bool M(Map map) => map.Keys.Contains("key"); }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies an explicit implementation does not expose a directly callable class method.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExplicitImplementationIsCleanOnClassAsync()
    {
        const string Source = """
            using System.Collections.Generic;
            public interface IMap
            {
                List<string> Keys { get; }
                bool ContainsKey(string key);
            }
            public class Map : IMap
            {
                public List<string> Keys => null;
                bool IMap.ContainsKey(string key) => false;
            }
            public class C
            {
                public bool M(Map map) => map.Keys.Contains("key");
                public bool N(IMap map) => map.Keys.{|PSH1407:Contains|}("key");
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies interfaces and type parameters without a matching inherited method remain silent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InterfaceWithoutReplacementIsCleanAsync()
    {
        const string Source = """
            using System.Collections.Generic;
            public interface IEmpty { }
            public interface IKeys : IEmpty { List<string> Keys { get; } }
            public class C
            {
                public bool M(IKeys map) => map.Keys.Contains("key");
                public bool N<T>(T map) where T : IKeys => map.Keys.Contains("key");
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies Keys must bind to an instance property rather than a field or static property.</summary>
    /// <param name="keysMember">The lookalike Keys member.</param>
    /// <param name="receiver">The receiver used to access it.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public List<string> Keys;", "map")]
    [Arguments("public static List<string> Keys => null;", "Map")]
    public async Task NonInstanceKeysPropertyIsCleanAsync(string keysMember, string receiver)
    {
        var source = $$"""
            using System.Collections.Generic;
            public class Map
            {
                {{keysMember}}
                public bool ContainsKey(string key) => false;
            }
            public class C { public bool M(Map map) => {{receiver}}.Keys.Contains("key"); }
            """;
        await VerifyAsync(source);
    }

    /// <summary>Verifies broken calls and missing key views are not reported.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnresolvedContainsIsCleanAsync()
    {
        const string Source = """
            using System.Collections.Generic;
            public class C
            {
                public bool M(Dictionary<string, int> map) => map.Keys.Contains({|CS1503:42|});
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies different invocation syntax, collection members, and explicit comparers remain intact.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NearMissCallsAreCleanAsync()
    {
        const string Source = """
            using System.Collections.Generic;
            using System.Linq;
            public class C
            {
                public void M(Dictionary<string, int> map, string key)
                {
                    _ = map.ContainsKey(key);
                    _ = map.Values.Contains(1);
                    _ = map.Keys.Count();
                    _ = map.Keys.Contains(key, System.StringComparer.OrdinalIgnoreCase);
                    _ = Enumerable.Contains(map.Keys, key);
                    _ = (map.Keys).Contains(key);
                    _ = map.Keys?.Contains(key);
                    var keys = map.Keys;
                    _ = keys.Contains(key);
                }
            }
            """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies all syntactic rejection paths and the extracted key-view receiver.</summary>
    /// <param name="expression">The invocation syntax.</param>
    /// <param name="matches">Whether the chain should be recognized.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("map.Keys.Contains(key)", true)]
    [Arguments("map.Keys.Contains()", false)]
    [Arguments("map.Keys.Contains(key, comparer)", false)]
    [Arguments("Contains(key)", false)]
    [Arguments("map.Keys.Other(key)", false)]
    [Arguments("keys.Contains(key)", false)]
    [Arguments("map.Values.Contains(key)", false)]
    public async Task KeysContainsShapeIsClassifiedAsync(string expression, bool matches)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(expression);
        var actual = Psh1407UseContainsKeyOverKeysContainsAnalyzer.IsKeysContainsShape(invocation, out var keys);
        await Assert.That(actual).IsEqualTo(matches);
        await Assert.That(keys?.ToString()).IsEqualTo(matches ? "map.Keys" : null);
    }

    /// <summary>Verifies a custom map is ignored when the framework has no generic dictionary interface.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingDictionaryFrameworkIsCleanAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = """
                namespace System
                {
                    public class Object { }
                    public class ValueType { }
                    public struct Void { }
                    public struct Boolean { }
                    public struct Int32 { }
                }
                public class KeysView { public bool Contains(int key) => false; }
                public class Map
                {
                    public KeysView Keys => null;
                    public bool ContainsKey(int key) => false;
                }
                public class C
                {
                    public bool M(Map map) => map.Keys.Contains(1);
                    public bool N(Map map) => map.Keys.Contains(2);
                }
                """,
        };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectMetadataReferences(projectId, []));
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
