// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;

using VerifyPropertyCopy = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1017PropertyCopiesCollectionAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1017PropertyCopiesCollectionAnalyzer"/> (PSH1017 property copies a collection).</summary>
public class PropertyCopiesCollectionAnalyzerUnitTest
{
    /// <summary>Verifies collection snapshots stored by auto-properties allocate only during initialization.</summary>
    /// <param name="initialization">The snapshot storage and initialization.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public List<int> Items { get; } = Names.Select(static value => value * 2).ToList();")]
    [Arguments("public List<int> Items { get; } public C() => Items = Names.Select(static value => value * 2).ToList();")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CachedAutoPropertyIsCleanAsync(string initialization) =>
        VerifyAsync(
            $$"""
            using System.Collections.Generic;
            using System.Linq;
            public class C
            {
                private static readonly int[] Names = { 1, 2 };
                {{initialization}}
            }
            """);

    /// <summary>Verifies an expression-bodied property ending in ToArray is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExpressionBodiedToArrayIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly List<int> _items = new();

                public int[] {|PSH1017:Items|} => _items.ToArray();
            }
            """);

    /// <summary>Verifies a property ending in ToList is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ToListIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly int[] _items = new int[4];

                public List<int> {|PSH1017:Items|} => _items.ToList();
            }
            """);

    /// <summary>Verifies a property ending in ToHashSet is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ToHashSetIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly List<int> _items = new();

                public HashSet<int> {|PSH1017:Unique|} => _items.ToHashSet();
            }
            """);

    /// <summary>Verifies a property ending in ToDictionary is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ToDictionaryIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly List<int> _items = new();

                public Dictionary<int, int> {|PSH1017:ByValue|} => _items.ToDictionary(x => x, x => x);
            }
            """);

    /// <summary>Verifies an array Clone behind the cast it always needs is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArrayCloneIsReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                private readonly int[] _values = new int[4];

                public int[] {|PSH1017:Values|} => (int[])_values.Clone();
            }
            """);

    /// <summary>Verifies a collection seeded from a field through its constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SeedingConstructorIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                private readonly List<int> _items = new();

                public List<int> {|PSH1017:Copy|} => new List<int>(_items);
            }
            """);

    /// <summary>Verifies an inline array built on every read is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InlineArrayIsReportedAsync() =>
        VerifyAsync(
            """
            public class C
            {
                private readonly int _first;
                private readonly int _second;

                public int[] {|PSH1017:Pair|} => new[] { _first, _second };
            }
            """);

    /// <summary>Verifies a block-bodied getter that returns a copy is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BlockBodiedGetterIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly List<int> _items = new();

                public int[] {|PSH1017:Items|}
                {
                    get
                    {
                        return _items.ToArray();
                    }
                }
            }
            """);

    /// <summary>Verifies a copy returned from inside a branch of a block-bodied getter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BranchedReturnIsReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly List<int> _items = new();
                private readonly bool _flag;

                public int[] {|PSH1017:Items|}
                {
                    get
                    {
                        if (_flag)
                        {
                            return _items.ToArray();
                        }

                        return System.Array.Empty<int>();
                    }
                }
            }
            """);

    /// <summary>Verifies an auto-property stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AutoPropertyIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public int[] Items { get; } = new int[4];
            }
            """);

    /// <summary>Verifies a property that hands back the field itself stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CachedFieldIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                private readonly List<int> _items = new();

                public IReadOnlyList<int> Items => _items;
            }
            """);

    /// <summary>Verifies a copy that is built once and cached stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CachedCopyIsCleanAsync() =>
        VerifyAsync(
            """
            #nullable enable
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly List<int> _items = new();
                private int[]? _cache;

                public int[] Items => _cache ??= _items.ToArray();
            }
            """);

    /// <summary>Verifies a property returning an existing immutable snapshot stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImmutableSnapshotFieldIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Collections.Immutable;

            public class C
            {
                private readonly ImmutableArray<int> _items = ImmutableArray<int>.Empty;

                public ImmutableArray<int> Items => _items;
            }
            """);

    /// <summary>Verifies an indexer that copies is not this rule's business.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IndexerIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly List<int> _items = new();

                public int[] this[int index] => _items.ToArray();
            }
            """);

    /// <summary>Verifies a copy made by a set accessor is not this rule's business.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SetAccessorIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private int[] _cache = new int[4];

                public int[] Items
                {
                    get => _cache;
                    set => _cache = value.ToList().ToArray();
                }
            }
            """);

    /// <summary>Verifies a capacity constructor stays clean; it sizes a collection, it does not copy one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CapacityConstructorIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public List<int> Scratch => new List<int>(8);
            }
            """);

    /// <summary>Verifies a numeric literal converted to a collection source still reports a copying constructor.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NumericLiteralConvertedToCollectionIsReportedAsync() =>
        VerifyAsync(
            """
            namespace System.Collections.Generic
            {
                public sealed class Copy : List<int>
                {
                    public Copy(Source source) : base(source) { }
                }

                public sealed class Source : List<int>
                {
                    public static implicit operator Source(int value) => new Source();
                }
            }

            public class C
            {
                public System.Collections.Generic.Copy {|PSH1017:Items|} => new System.Collections.Generic.Copy(8);
            }
            """);

    /// <summary>Verifies a read-only wrapper stays clean; it wraps the list instead of copying it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReadOnlyWrapperIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            using System.Collections.ObjectModel;

            public class C
            {
                private readonly List<int> _items = new();

                public IReadOnlyList<int> Items => new ReadOnlyCollection<int>(_items);
            }
            """);

    /// <summary>Verifies a copy made inside a lambda in the getter is not mistaken for the property's result.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CopyInsideLambdaIsCleanAsync() =>
        VerifyAsync(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly List<int> _items = new();

                public IEnumerable<int> Items
                {
                    get
                    {
                        Func<int[]> make = () => _items.ToArray();
                        return make();
                    }
                }
            }
            """);

    /// <summary>Verifies an excluded property name is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExcludedPropertyIsCleanAsync() =>
        VerifyWithConfigAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly List<int> _items = new();

                public int[] Items => _items.ToArray();
            }
            """,
            "performancesharp.PSH1017.excluded_properties = Items, Other");

    /// <summary>Verifies excluding a different name leaves the property reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedExclusionStillReportsAsync() =>
        VerifyWithConfigAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                private readonly List<int> _items = new();

                public int[] {|PSH1017:Items|} => _items.ToArray();
            }
            """,
            "performancesharp.PSH1017.excluded_properties = Other");

    /// <summary>Verifies only the supported return-expression shapes enter semantic analysis.</summary>
    /// <param name="expression">The returned expression.</param>
    /// <param name="matches">Whether the syntax can allocate a collection copy.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("items.ToArray()", true)]
    [Arguments("items.ToList()", true)]
    [Arguments("items.ToHashSet()", true)]
    [Arguments("items.ToDictionary()", true)]
    [Arguments("items.Clone()", true)]
    [Arguments("items.Select(x => x)", false)]
    [Arguments("ToArray()", false)]
    [Arguments("new List<int>(items)", true)]
    [Arguments("new(items)", true)]
    [Arguments("new List<int>()", false)]
    [Arguments("new List<int> { 1 }", false)]
    [Arguments("new int[2]", false)]
    [Arguments("new int[] { 1 }", true)]
    [Arguments("new[] { 1 }", true)]
    [Arguments("items", false)]
    public async Task CopySyntaxRecognizesOnlySupportedShapesAsync(string expression, bool matches)
    {
        var actual = Psh1017PropertyCopiesCollectionAnalyzer.IsCopyShape(SyntaxFactory.ParseExpression(expression));
        await Assert.That(actual).IsEqualTo(matches);
    }

    /// <summary>Verifies getter scans traverse statement containers and unwrap casts and parentheses.</summary>
    /// <param name="getter">The getter statements.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("return ((int[])(new int[] { 1 }));")]
    [Arguments("if (flag) return items; else return new[] { 1 };")]
    [Arguments("try { return items; } catch { return new[] { 1 }; }")]
    [Arguments("try { return new[] { 1 }; } finally { _ = items.Length; }")]
    [Arguments("switch (items.Length) { case 0: return items; default: return new[] { 1 }; }")]
    [Arguments("while (flag) { return new[] { 1 }; } return items;")]
    [Arguments("lock (items) { return new[] { 1 }; }")]
    [Arguments("int[] Make() { return items; } _ = Make(); return new[] { 1 };")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NestedCopyReturnIsReportedAsync(string getter) =>
        VerifyAsync(
            $$"""
            class C
            {
                bool flag;
                int[] items = new int[0];
                public int[] {|PSH1017:Items|} { get { {{getter}} } }
            }
            """);

    /// <summary>Verifies returns belonging to nested functions do not become property copies.</summary>
    /// <param name="getter">The getter statements with no directly returned allocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int[] Make() { return new[] { 1 }; } return Make();")]
    [Arguments("System.Func<int[]> make = delegate { return new[] { 1 }; }; return make();")]
    [Arguments("try { return items; } finally { _ = new[] { 1 }; }")]
    [Arguments("if (items.Length == 0) return items; return items;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task GetterWithoutReturnedCopyIsCleanAsync(string getter) =>
        VerifyAsync(
            $$"""
            class C
            {
                int[] items = new int[0];
                public int[] Items { get { {{getter}} } }
            }
            """);

    /// <summary>Verifies accessor arrows and target-typed constructors report their collection allocations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CollectionReturnAndAccessorShapesAreReportedAsync() =>
        VerifyAsync(
            """
            using System.Collections;
            using System.Collections.Generic;
            using System.Collections.Concurrent;
            class C
            {
                int[] items = new int[0];
                public int[] {|PSH1017:Arrow|} { set { } get => new[] { 1 }; }
                public List<int> {|PSH1017:TargetTyped|} => new(items);
                public ConcurrentBag<int> {|PSH1017:Concurrent|} => new ConcurrentBag<int>(items);
                public IEnumerable {|PSH1017:NonGeneric|} => new[] { 1 };
                public IEnumerable<int> {|PSH1017:Generic|} => new[] { 1 };
                public List<int> {|PSH1017:Named|} => new List<int>(collection: items);
            }
            """);

    /// <summary>Verifies nullable collections and generic element types retain the same copy behavior.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NullableAndGenericCollectionCopiesAreReportedAsync() =>
        VerifyAsync(
            """
            #nullable enable
            using System.Collections.Generic;
            using System.Linq;
            class C<T>
            {
                T[] items = new T[0];
                public T[]? {|PSH1017:Items|} => items.ToArray();
                public IEnumerable<T> {|PSH1017:Sequence|} => new List<T>(items);
            }
            """);

    /// <summary>Verifies property types and copying-call return types must themselves be collections.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonCollectionPropertiesAndCopyMethodsAreCleanAsync() =>
        VerifyAsync(
            """
            using System;
            using System.Collections;
            using System.Collections.Generic;
            class Source
            {
                public string ToArray() => "";
                public object ToList() => null;
                public int[] Clone() => new int[0];
            }
            class C
            {
                Source source = new Source();
                public object Object => new int[] { 1 };
                public string Text => source.ToArray();
                public Span<int> Span => new int[] { 1 };
                public IEnumerable Cast => (IEnumerable)source.ToList();
                public int[] Clone => source.Clone();
                public List<int> Capacity => new(8);
                public int[] SetOnly { set { } }
            }
            """);

    /// <summary>Verifies primitive capacity arguments do not cause copying-constructor reports.</summary>
    /// <param name="argument">The capacity argument syntax.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("8")]
    [Arguments("'a'")]
    [Arguments("capacity: 8")]
    [Arguments("capacity")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CapacityArgumentShapesAreCleanAsync(string argument) =>
        VerifyAsync(
            $$"""
            using System.Collections.Generic;
            class C
            {
                int capacity = 8;
                public List<int> Items => new List<int>({{argument}});
            }
            """);

    /// <summary>Verifies generic and expanded-array constructor parameters survive the primitive prefilter.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task GenericAndParamsConstructorParametersAreClassifiedAsync() =>
        VerifyAsync(
            """
            namespace System.Collections.Generic
            {
                public class Copy<T> : List<int>
                {
                    public Copy(T source) { }
                }
                public class ExpandedCopy : List<int>
                {
                    public ExpandedCopy(params int[] source) { }
                }
                public class BooleanCapacity : List<int>
                {
                    public BooleanCapacity() { }
                    public BooleanCapacity(bool enabled) { }
                }
                public class Source : List<int>
                {
                    public static implicit operator Source(int value) => new Source();
                }
            }
            class C
            {
                public System.Collections.Generic.Copy<System.Collections.Generic.Source> {|PSH1017:GenericCopy|} => new System.Collections.Generic.Copy<System.Collections.Generic.Source>(1);
                public System.Collections.Generic.Copy<int> Scalar => new System.Collections.Generic.Copy<int>(1);
                public System.Collections.Generic.ExpandedCopy {|PSH1017:Expanded|} => new System.Collections.Generic.ExpandedCopy(1);
                public System.Collections.Generic.BooleanCapacity True => new System.Collections.Generic.BooleanCapacity(true);
                public System.Collections.Generic.BooleanCapacity False => new System.Collections.Generic.BooleanCapacity(false);
            }
            """);

    /// <summary>Verifies copying-looking constructors outside the supported namespaces stay clean.</summary>
    /// <param name="typeNamespace">The collection's namespace.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Example")]
    [Arguments("Example.Generic")]
    [Arguments("Example.Collections.Generic")]
    [Arguments("System.Other.Generic")]
    [Arguments("Example.System.Collections.Generic")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ForeignCollectionNamespacesAreCleanAsync(string typeNamespace) =>
        VerifyAsync(
            $$"""
            namespace {{typeNamespace}}
            {
                public class Copy : global::System.Collections.Generic.List<int>
                {
                    public Copy(global::System.Collections.Generic.IEnumerable<int> items) { }
                }
            }
            class C
            {
                int[] items = new int[0];
                public {{typeNamespace}}.Copy Explicit => new {{typeNamespace}}.Copy(items);
                public {{typeNamespace}}.Copy Implicit => new(items);
            }
            """);

    /// <summary>Verifies an empty exclusion setting reports every copying property in the same tree.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EmptyExclusionStillReportsBothPropertiesAsync() =>
        VerifyWithConfigAsync(
            """
            class C
            {
                public int[] {|PSH1017:First|} => new[] { 1 };
                public int[] {|PSH1017:Second|} => new[] { 2 };
            }
            """,
            "performancesharp.PSH1017.excluded_properties = ");

    /// <summary>Verifies unresolved materializers and constructor overloads do not report a copy.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnresolvedCopyCallsAreCleanAsync() =>
        VerifyAsync(
            """
            using System.Collections.Generic;
            class C
            {
                List<int> items = new List<int>();
                public int[] Array => items.{|CS1501:ToArray|}(1);
                public List<int> List => new List<int>({|CS1503:(object)items|});
                public List<int> Implicit => new({|CS1503:(object)items|});
            }
            """);

    /// <summary>Verifies construction without a constructor constraint does not report a collection copy.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TypeParameterConstructorIsIgnoredAsync()
    {
        const string Source = """
            class C<T> where T : System.Collections.IEnumerable
            {
                int[] items = new int[0];
                public T Items => new T(items);
            }
            """;
        var compilation = CSharpCompilation.Create(
            "TypeParameterConstructor",
            [CSharpSyntaxTree.ParseText(Source)],
            [RuntimeMetadataReferences.CoreLibrary],
            new(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        await Assert.That(errors.Length).IsEqualTo(1);
        await Assert.That(errors[0].Id).IsEqualTo("CS0304");
        var diagnostics = await compilation.WithAnalyzers([new Psh1017PropertyCopiesCollectionAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a getter return missing its required expression does not report a copy.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetterReturnWithoutExpressionIsIgnoredAsync()
    {
        const string Source = "class C { public int[] Items { get { return; } } }";
        var compilation = CSharpCompilation.Create(
            "MissingReturnExpression",
            [CSharpSyntaxTree.ParseText(Source)],
            [RuntimeMetadataReferences.CoreLibrary],
            new(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        await Assert.That(errors.Length).IsEqualTo(1);
        await Assert.That(errors[0].Id).IsEqualTo("CS0126");
        var diagnostics = await compilation.WithAnalyzers([new Psh1017PropertyCopiesCollectionAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyPropertyCopy.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer verification with one editorconfig setting applied.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="setting">The editorconfig line to apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithConfigAsync(string source, string setting)
    {
        var test = new VerifyPropertyCopy.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", $"""
            root = true
            [*.cs]
            {setting}

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
