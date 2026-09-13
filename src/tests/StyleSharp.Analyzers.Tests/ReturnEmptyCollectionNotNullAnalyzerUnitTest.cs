// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;

using VerifyEmptyCollection = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2306ReturnEmptyCollectionNotNullAnalyzer,
    StyleSharp.Analyzers.Sst2306ReturnEmptyCollectionNotNullCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2306 (return an empty collection instead of null) and its fix.</summary>
public class ReturnEmptyCollectionNotNullAnalyzerUnitTest
{
    /// <summary>Verifies nonconstructible collection shapes report without offering an invalid replacement.</summary>
    /// <param name="source">The collection-returning source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { int[,] M() => {|SST2306:null|}; }")]
    [Arguments("using System.Collections; class C { ICollection M() => {|SST2306:null|}; }")]
    [Arguments("using System.Collections; class C { IList M() => {|SST2306:null|}; }")]
    [Arguments("using System.Collections.Generic; interface ICustom<T> : IEnumerable<T> { } class C { ICustom<int> M() => {|SST2306:null|}; }")]
    [Arguments("using System.Collections; interface ICustom : IEnumerable { } class C { ICustom M() => {|SST2306:null|}; }")]
    [Arguments("using System.Collections; abstract class Items : IEnumerable { public abstract IEnumerator GetEnumerator(); } class C { Items M() => {|SST2306:null|}; }")]
    [Arguments("using System.Collections; class Items : IEnumerable { private Items() { } public IEnumerator GetEnumerator() => null; } class C { Items M() => {|SST2306:null|}; }")]
    [Arguments("using System.Collections; class Items : IEnumerable { public Items(int value) { } public IEnumerator GetEnumerator() => null; } class C { Items M() => {|SST2306:null|}; }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnconstructibleCollectionReportsWithoutFixAsync(string source) =>
        VerifyEmptyCollection.VerifyCodeFixAsync(source, source);

    /// <summary>Verifies nongeneric and readonly interfaces get constructible empty values.</summary>
    /// <param name="type">The declared collection type.</param>
    /// <param name="replacement">The expected empty expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("System.Collections.IEnumerable", "System.Array.Empty<object>()")]
    [Arguments("System.Collections.Generic.ICollection<int>", "System.Array.Empty<int>()")]
    [Arguments("System.Collections.Generic.IList<int>", "System.Array.Empty<int>()")]
    [Arguments("System.Collections.Generic.IReadOnlyCollection<int>", "System.Array.Empty<int>()")]
    [Arguments("System.Collections.Generic.IReadOnlySet<int>", "new System.Collections.Generic.HashSet<int>()")]
    [Arguments("System.Collections.Generic.IReadOnlyDictionary<string, int>", "new System.Collections.Generic.Dictionary<string, int>()")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InterfaceUsesCompatibleEmptyValueAsync(string type, string replacement) =>
        VerifyEmptyCollection.VerifyCodeFixAsync(
            $"class C {{ {type} M() => {{|SST2306:null|}}; }}",
            $"class C {{ {type} M() => {replacement}; }}");

    /// <summary>Verifies parentheses and both null conditional branches retain their return context.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ParenthesizedConditionalNullsAreFixedAsync() =>
        VerifyEmptyCollection.VerifyCodeFixAsync(
            "class C { int[] M(bool flag) => (flag ? ({|SST2306:null|}) : ({|SST2306:null|})); }",
            "class C { int[] M(bool flag) => (flag ? ([]) : ([])); }");

    /// <summary>Verifies a pre-C# 9 conditional reports null without changing branch inference.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UntargetedConditionalHasNoFixAsync()
    {
        const string Source = "class C { int[] M(bool flag) => flag ? new int[0] : {|SST2306:null|}; }";
        var test = new VerifyEmptyCollection.Test { TestCode = Source, FixedCode = Source, ReferenceAssemblies = AnalyzerFrameworks.Net80 };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.CSharp8)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies returns without collection ownership or null branches are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NoncollectionReturnContextsAreCleanAsync() =>
        VerifyEmptyCollection.VerifyAnalyzerAsync("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            class C
            {
                void Stop() { return; }
                T Generic<T>() where T : class, IEnumerable<int> => null;
                int[] Choose(bool flag) => flag ? new int[0] : new int[1];
                Func<int[]> Factory() => () => { return null; };
                Func<int[]> Anonymous() => delegate { return null; };
                public static implicit operator int[](C value) => null;
                Task<int[]> Run()
                {
                    async Task<int[]> Inner() { await Task.Yield(); return null; }
                    return Inner();
                }
            }
            """);

    /// <summary>Verifies unsupported element types and mismatched generic arity never produce an invalid fix.</summary>
    /// <param name="returnType">The collection shape to inspect.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int*[]")]
    [Arguments("delegate*<void>[]")]
    [Arguments("System.Span<int>[]")]
    [Arguments("System.Collections.Generic.IEnumerable<int*>")]
    [Arguments("System.Collections.Generic.IEnumerable<delegate*<void>>")]
    [Arguments("System.Collections.Generic.IEnumerable<System.Span<int>>")]
    [Arguments("System.Collections.Generic.ISet<int, int>")]
    public async Task UnsupportedCollectionShapeReportsWithoutReplacementAsync(string returnType)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            namespace System.Collections.Generic { interface ISet<TFirst, TSecond> : System.Collections.IEnumerable { } }
            unsafe class C { {{returnType}} M() => null; }
            """);
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform, new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var diagnostics = await compilation.WithAnalyzers([new Sst2306ReturnEmptyCollectionNotNullAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST2306");
        await Assert.That(diagnostics[0].Properties.ContainsKey(Sst2306ReturnEmptyCollectionNotNullAnalyzer.ReplacementKey)).IsFalse();
    }

    /// <summary>Verifies missing framework factories use zero-length arrays or withhold collection construction.</summary>
    /// <param name="returnType">The collection returned by the test method.</param>
    /// <param name="replacement">The expected fallback, or null when no fix is possible.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int[]", "new int[0]")]
    [Arguments("System.Collections.IEnumerable", "new object[0]")]
    [Arguments("System.Collections.Generic.IEnumerable<int>", "new int[0]")]
    [Arguments("System.Collections.Generic.ISet<int>", null)]
    [Arguments("System.Collections.Generic.IDictionary<int, int>", null)]
    [Arguments("System.Collections.Generic.ICustom<int>", null)]
    public async Task MissingFrameworkConstructionUsesSafeFallbackAsync(string returnType, string? replacement)
    {
        var source = $$"""
            namespace System
            {
                public class Object { }
                public class ValueType { }
                public struct Void { }
                public struct Boolean { }
                public struct Int32 { }
                public class String { }
                public class Array { }
            }
            namespace System.Collections { public interface IEnumerable { } }
            namespace System.Collections.Generic
            {
                public interface IEnumerable<T> : System.Collections.IEnumerable { }
                public interface ISet<T> : IEnumerable<T> { }
                public interface IDictionary<TKey, TValue> : IEnumerable<TKey> { }
                public interface ICustom<T> : IEnumerable<T> { }
            }
            class C { {{returnType}} M() => null; }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, new(LanguageVersion.CSharp11));
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], options: new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Sst2306ReturnEmptyCollectionNotNullAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST2306");
        _ = diagnostics[0].Properties.TryGetValue(Sst2306ReturnEmptyCollectionNotNullAnalyzer.ReplacementKey, out var actual);
        await Assert.That(actual).IsEqualTo(replacement);
    }

    /// <summary>Verifies incomplete return contexts are ignored without analyzer failures.</summary>
    /// <param name="source">The malformed or unresolved source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { Missing M() => null; }")]
    [Arguments("class C { int[] P { set { return null; } } }")]
    [Arguments("class C { event System.Action E { get { return null; } } }")]
    [Arguments("return null;")]
    public async Task IncompleteReturnContextIsIgnoredAsync(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform, new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Sst2306ReturnEmptyCollectionNotNullAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies an array-returning method's null becomes an empty collection expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayReturnIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int[] Find(bool found)
                                  {
                                      if (!found)
                                      {
                                          return {|SST2306:null|};
                                      }

                                      return new int[1];
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int[] Find(bool found)
                                       {
                                           if (!found)
                                           {
                                               return [];
                                           }

                                           return new int[1];
                                       }
                                   }
                                   """;
        await VerifyEmptyCollection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an expression-bodied member returning null is fixed with the empty array every framework has.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodyIsFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public IEnumerable<string> Names => {|SST2306:null|};

                                  public IReadOnlyList<int> Values() => {|SST2306:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public IEnumerable<string> Names => Array.Empty<string>();

                                       public IReadOnlyList<int> Values() => Array.Empty<int>();
                                   }
                                   """;
        await VerifyEmptyCollection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix names the empty array the way the file's imports require, not the way it reads best.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReplacementIsQualifiedWhenTheNamespaceIsNotImportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public IEnumerable<int> Values() => {|SST2306:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public IEnumerable<int> Values() => System.Array.Empty<int>();
                                   }
                                   """;
        await VerifyEmptyCollection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a concrete collection type is constructed rather than emptied through an array.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcreteCollectionIsConstructedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public List<int> Items() => {|SST2306:null|};

                                  public Dictionary<string, int> Map() => {|SST2306:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public List<int> Items() => new List<int>();

                                       public Dictionary<string, int> Map() => new Dictionary<string, int>();
                                   }
                                   """;
        await VerifyEmptyCollection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a set or dictionary interface gets the concrete type an empty array cannot stand in for.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeyedInterfacesGetTheirConcreteTypeAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public ISet<int> Tags() => {|SST2306:null|};

                                  public IDictionary<string, int> Map() => {|SST2306:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public ISet<int> Tags() => new HashSet<int>();

                                       public IDictionary<string, int> Map() => new Dictionary<string, int>();
                                   }
                                   """;
        await VerifyEmptyCollection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a null branch of a returned conditional is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalBranchIsFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  private readonly List<int> _items = new List<int>();

                                  public IReadOnlyList<int> Items(bool enabled) => enabled ? _items : {|SST2306:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       private readonly List<int> _items = new List<int>();

                                       public IReadOnlyList<int> Items(bool enabled) => enabled ? _items : Array.Empty<int>();
                                   }
                                   """;
        await VerifyEmptyCollection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a property getter and an indexer getter are measured like a method.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GettersAreMeasuredAsync() =>
        VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public List<int> Items
                {
                    get { return {|SST2306:null|}; }
                }

                public int[] this[int index] => {|SST2306:null|};
            }
            """);

    /// <summary>Verifies a nullable return type is the author saying null is a value here.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullableReturnTypeIsCleanAsync() =>
        VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            #nullable enable
            using System.Collections.Generic;

            public sealed class C
            {
                public IEnumerable<int>? Sequence() => null;

                public int[]? Values() => null;
            }
            """);

    /// <summary>Verifies a string is never treated as a collection of characters.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StringReturnIsCleanAsync() =>
        VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string Name() => null;

                public string Text
                {
                    get { return null; }
                }
            }
            """);

    /// <summary>Verifies a task of a collection is left to the rules that own asynchronous returns.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TaskOfCollectionIsCleanAsync() =>
        VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public sealed class C
            {
                public Task<List<int>> ItemsAsync() => null;

                public async Task<List<int>> LoadAsync()
                {
                    await Task.Yield();
                    return null;
                }

                public ValueTask<int[]> ValuesAsync() => default;
            }
            """);

    /// <summary>Verifies a lambda takes its shape from its delegate, and a scalar is not a collection.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LambdasAndScalarsAreCleanAsync() =>
        VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class C
            {
                public object Scalar() => null;

                public Func<List<int>> Factory()
                {
                    return () => null;
                }
            }
            """);

    /// <summary>Verifies a member with no body has nothing to return.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AbstractAndInterfaceMembersAreCleanAsync() =>
        VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public interface IStore
            {
                IReadOnlyList<int> Items();
            }

            public abstract class Base
            {
                public abstract int[] Values();
            }
            """);

    /// <summary>Verifies a local function is measured, and a non-empty return is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalFunctionIsMeasuredAsync() =>
        VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int[] Run()
                {
                    int[] Inner()
                    {
                        return {|SST2306:null|};
                    }

                    return Inner();
                }
            }
            """);

    /// <summary>Verifies the shared empty array is written where the language has no collection expressions.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BelowCSharp12TheEmptyArrayIsWrittenAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public int[] Values()
                                  {
                                      return {|SST2306:null|};
                                  }

                                  public IEnumerable<string> Names()
                                  {
                                      return {|SST2306:null|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public int[] Values()
                                       {
                                           return Array.Empty<int>();
                                       }

                                       public IEnumerable<string> Names()
                                       {
                                           return Array.Empty<string>();
                                       }
                                   }
                                   """;
        var test = new VerifyEmptyCollection.Test { TestCode = Source, FixedCode = FixedSource, };

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp11));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
