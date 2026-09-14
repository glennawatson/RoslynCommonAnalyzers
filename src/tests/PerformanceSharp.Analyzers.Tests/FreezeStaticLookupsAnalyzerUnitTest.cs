// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1114FreezeStaticLookupsAnalyzer,
    PerformanceSharp.Analyzers.Psh1114FreezeStaticLookupsCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1114FreezeStaticLookupsAnalyzer"/> (PSH1114 frozen lookups, opt-in).</summary>
public class FreezeStaticLookupsAnalyzerUnitTest
{
    /// <summary>The path the analyzer config file is added at in the test workspace.</summary>
    private const string EditorConfigPath = "/.editorconfig";

    /// <summary>The editorconfig that opts into the disabled-by-default rule.</summary>
    private const string OptInConfig = """
        root = true

        [*.cs]
        dotnet_diagnostic.PSH1114.severity = warning
        """;

    /// <summary>Verifies the type-name gate handles qualification and rejects unrelated syntax.</summary>
    /// <param name="type">The declared type syntax.</param>
    /// <param name="expected">The accepted lookup name, or null.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("global::Dictionary<int, int>", "Dictionary")]
    [Arguments("System.Collections.Generic.HashSet<int>", "HashSet")]
    [Arguments("List<int>", null)]
    [Arguments("int", null)]
    [Arguments("Lookup", null)]
    [Arguments("Dictionary<int, int>[]", null)]
    public async Task LookupTypeSyntaxIsClassifiedAsync(string type, string? expected)
    {
        var result = Psh1114FreezeStaticLookupsAnalyzer.TryGetLookupTypeName(SyntaxFactory.ParseTypeName(type));
        await Assert.That(result?.Identifier.ValueText).IsEqualTo(expected);
    }

    /// <summary>Verifies each accepted read form permits freezing, including qualified references.</summary>
    /// <param name="body">The read operation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("_ = Lookup.Count;")]
    [Arguments("_ = Lookup.ContainsKey(1);")]
    [Arguments("_ = Lookup.GetEnumerator();")]
    [Arguments("_ = Lookup[1];")]
    [Arguments("int value; value = Lookup[1];")]
    [Arguments("_ = C.Lookup.Count;")]
    [Arguments("foreach (var pair in Lookup) { }")]
    [Arguments("foreach (var (key, value) in Lookup) { }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task WhitelistedReadsAreReportedAsync(string body) =>
        VerifyOptInAsync($$"""
            using System.Collections.Generic;
            class C
            {
                static readonly Dictionary<int, int> {|PSH1114:Lookup|} = new();
                void M() { {{body}} }
            }
            """);

    /// <summary>Verifies writes, escaping references, and non-whitelisted members prevent freezing.</summary>
    /// <param name="body">The lookup usage.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Lookup[1] = 2;")]
    [Arguments("Lookup[1] += 2;")]
    [Arguments("_ = Lookup.Keys;")]
    [Arguments("_ = Lookup.Values;")]
    [Arguments("Lookup.Clear();")]
    [Arguments("System.Func<int, bool> contains = Lookup.ContainsKey;")]
    [Arguments("var array = new object[1]; _ = array[Lookup.Count]; _ = new Dictionary<object, int>()[Lookup];")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonWhitelistedUsesAreCleanAsync(string body) =>
        VerifyOptInAsync($$"""
            using System.Collections.Generic;
            class C
            {
                static readonly Dictionary<int, int> Lookup = new();
                void M() { {{body}} }
            }
            """);

    /// <summary>Verifies only private static readonly initialized single lookup fields are candidates.</summary>
    /// <param name="field">The non-candidate field declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("static readonly Dictionary<int, int> First = new(), Second = new();")]
    [Arguments("static readonly Dictionary<int, int> Lookup;")]
    [Arguments("readonly Dictionary<int, int> Lookup = new();")]
    [Arguments("static Dictionary<int, int> Lookup = new();")]
    [Arguments("public static readonly Dictionary<int, int> Lookup = new();")]
    [Arguments("protected static readonly Dictionary<int, int> Lookup = new();")]
    [Arguments("static readonly List<int> Lookup = new();")]
    [Arguments("static readonly int Lookup = 1;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonCandidateFieldsAreCleanAsync(string field) =>
        VerifyOptInAsync($"using System.Collections.Generic; class C {{ {field} }}");

    /// <summary>Verifies a same-named user type does not bind to the framework dictionary.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UserDefinedDictionaryIsCleanAsync() =>
        VerifyOptInAsync("class Dictionary<K, V> { } class C { static readonly Dictionary<int, int> Lookup = new(); }");

    /// <summary>Verifies unbound lookup names and fields outside types are safely ignored.</summary>
    /// <param name="source">The incomplete declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { static readonly Dictionary<int, int> Lookup = new(); }")]
    [Arguments("namespace N { static readonly System.Collections.Generic.Dictionary<int, int> Lookup = new(); }")]
    public async Task IncompleteLookupDeclarationsAreCleanAsync(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, OptInConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a matching identifier in a foreach target or type makes the syntax scan conservative.</summary>
    /// <param name="loop">The loop containing the matching identifier outside its source expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("foreach (Lookup item in new int[0]) { }")]
    [Arguments("foreach (Lookup in new int[0]) { }")]
    public async Task ForeachNameOutsideSourcePreventsFreezingAsync(string loop)
    {
        var source = $$"""
            using Lookup = System.Int32;
            using System.Collections.Generic;
            class C
            {
                static readonly Dictionary<int, int> Lookup = new();
                void M() { {{loop}} }
            }
            """;
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, OptInConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies qualified declarations share resolved types and non-use name tokens do not count as escapes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MultipleQualifiedLookupsAreReportedAsync() =>
        VerifyOptInAsync("""
            class C
            {
                static readonly System.Collections.Generic.Dictionary<int, int> {|PSH1114:Lookup|} = new();
                static readonly System.Collections.Generic.HashSet<int> {|PSH1114:Values|} = new();
                void M(int Lookup) { }
            }
            """);

    /// <summary>Verifies a read-only lookup remains silent unless the diagnostic is enabled.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadOnlyLookupIsCleanWithoutOptInAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("using System.Collections.Generic; class C { static readonly Dictionary<int, int> Lookup = new(); }");
        var compilation = CSharpCompilation.Create("DefaultLookupOptions", [tree], RuntimeMetadataReferences.Platform, new(OutputKind.DynamicallyLinkedLibrary));
        await Assert.That(compilation.GetTypeByMetadataName("System.Collections.Frozen.FrozenDictionary")).IsNotNull();
        var diagnostics = await compilation.WithAnalyzers([new Psh1114FreezeStaticLookupsAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies frozen collections alone are insufficient when a lookup definition is missing.</summary>
    /// <param name="lookupDefinition">The one available generic lookup definition.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public class Dictionary<K, V> { }")]
    [Arguments("public class HashSet<T> { }")]
    public async Task MissingLookupDefinitionIsCleanAsync(string lookupDefinition)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            namespace System
            {
                public class Object { }
                public struct Void { }
                public struct Int32 { }
            }
            namespace System.Collections.Frozen { public static class FrozenDictionary { } }
            namespace System.Collections.Generic { {{lookupDefinition}} }
            class C
            {
                static readonly System.Collections.Generic.Dictionary<int, int> Lookup = new();
            }
            """);
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSpecificDiagnosticOptions([new KeyValuePair<string, ReportDiagnostic>("PSH1114", ReportDiagnostic.Warn)]);
        var compilation = CSharpCompilation.Create("MissingLookup", [tree], options: options);
        var diagnostics = await compilation.WithAnalyzers([new Psh1114FreezeStaticLookupsAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies the rule stays silent on a framework with no frozen collections.</summary>
    /// <param name="framework">The target framework being analyzed.</param>
    /// <param name="assemblies">That framework's reference assemblies.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [MethodDataSource(typeof(AnalyzerFrameworks), nameof(AnalyzerFrameworks.NetFrameworkOnly))]
    public async Task WithoutFrozenCollectionsIsCleanAsync(string framework, ReferenceAssemblies assemblies)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = assemblies,
            TestCode = $$"""
                         // analyzed as {{framework}}
                         using System.Collections.Generic;

                         public static class C
                         {
                             private static readonly Dictionary<string, int> Map = new Dictionary<string, int> { { "a", 1 } };

                             public static int Read(string key) => Map[key];
                         }
                         """,
        };

        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, OptInConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a read-only static dictionary is flagged and frozen by the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyDictionaryIsFlaggedAndFrozenAsync()
    {
        const string Source = """
                              using System.Collections.Frozen;
                              using System.Collections.Generic;

                              public class C
                              {
                                  private static readonly Dictionary<string, int> {|PSH1114:Lookup|} = new Dictionary<string, int>
                                  {
                                      ["one"] = 1,
                                  };

                                  public int M(string key) => Lookup.TryGetValue(key, out var value) ? value : 0;
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Frozen;
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       private static readonly FrozenDictionary<string, int> Lookup = new Dictionary<string, int>
                                       {
                                           ["one"] = 1,
                                       }.ToFrozenDictionary();

                                       public int M(string key) => Lookup.TryGetValue(key, out var value) ? value : 0;
                                   }
                                   """;
        await VerifyOptInAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comparer argument is carried through so lookup semantics never change.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparerIsCarriedThroughTheFreezeAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Frozen;
                              using System.Collections.Generic;

                              public class C
                              {
                                  private static readonly HashSet<string> {|PSH1114:Names|} = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                  {
                                      "one",
                                  };

                                  public bool M(string key) => Names.Contains(key);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Frozen;
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       private static readonly FrozenSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                       {
                                           "one",
                                       }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

                                       public bool M(string key) => Names.Contains(key);
                                   }
                                   """;
        await VerifyOptInAsync(Source, FixedSource);
    }

    /// <summary>Verifies a mutated dictionary stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MutatedDictionaryIsCleanAsync() =>
        VerifyOptInAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                private static readonly Dictionary<string, int> Lookup = new Dictionary<string, int>();

                public void M(string key) => Lookup.Add(key, 1);
            }
            """);

    /// <summary>Verifies a dictionary that escapes as an argument stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EscapingDictionaryIsCleanAsync() =>
        VerifyOptInAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                private static readonly Dictionary<string, int> Lookup = new Dictionary<string, int>();

                public void M() => Populate(Lookup);

                private static void Populate(Dictionary<string, int> target) => target["one"] = 1;
            }
            """);

    /// <summary>Verifies a non-private field stays clean; other assemblies' usage cannot be seen.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InternalFieldIsCleanAsync() =>
        VerifyOptInAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                internal static readonly Dictionary<string, int> Lookup = new Dictionary<string, int>();

                public int M(string key) => Lookup.TryGetValue(key, out var value) ? value : 0;
            }
            """);

    /// <summary>Verifies a partial type stays clean; another part could mutate the field.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PartialTypeIsCleanAsync() =>
        VerifyOptInAsync(
            """
            using System.Collections.Generic;

            public partial class C
            {
                private static readonly Dictionary<string, int> Lookup = new Dictionary<string, int>();

                public int M(string key) => Lookup.TryGetValue(key, out var value) ? value : 0;
            }
            """);

    /// <summary>Verifies a target-typed initializer keeps its type when it moves into the wrapper call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A target-typed <c>new</c> takes its type from where it sits. Moving it into an argument re-points it
    /// at the parameter, so it has to be given the type the field declared before it travels.
    /// </remarks>
    [Test]
    public async Task TargetTypedInitializerKeepsItsTypeAsync()
    {
        const string Source = """
            using System.Collections.Generic;

            public class C
            {
                private static readonly HashSet<int> {|PSH1114:N|} = new() { 1 };

                public bool M(int key) => N.Contains(key);
            }
            """;
        const string FixedSource = """
            using System.Collections.Generic;

            public class C
            {
                private static readonly global::System.Collections.Frozen.FrozenSet<int> N = global::System.Collections.Frozen.FrozenSet.ToFrozenSet(new HashSet<int>() { 1 });

                public bool M(int key) => N.Contains(key);
            }
            """;
        await VerifyOptInAsync(Source, FixedSource);
    }

    /// <summary>Verifies a target-typed initializer keeps its type on the fluent path too.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TargetTypedInitializerKeepsItsTypeWhenImportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Frozen;
                              using System.Collections.Generic;

                              public class C
                              {
                                  private static readonly HashSet<string> {|PSH1114:N|} = new(StringComparer.Ordinal) { "a" };

                                  public bool M(string key) => N.Contains(key);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Frozen;
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       private static readonly FrozenSet<string> N = new HashSet<string>(StringComparer.Ordinal) { "a" }.ToFrozenSet(StringComparer.Ordinal);

                                       public bool M(string key) => N.Contains(key);
                                   }
                                   """;
        await VerifyOptInAsync(Source, FixedSource);
    }

    /// <summary>Verifies the rule ships disabled by default; freezing only pays off for read-heavy tables.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleIsOffByDefaultAsync() =>
        await Assert.That(CollectionRules.FreezeStaticLookups.IsEnabledByDefault).IsFalse();

    /// <summary>Runs an opted-in verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyOptInAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, OptInConfig));
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
            test.FixedState.AnalyzerConfigFiles.Add((EditorConfigPath, OptInConfig));
        }

        await test.RunAsync(CancellationToken.None);
    }
}
