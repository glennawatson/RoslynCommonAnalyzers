// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyPropertyCopy = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1017PropertyCopiesCollectionAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1017PropertyCopiesCollectionAnalyzer"/> (PSH1017 property copies a collection).</summary>
public class PropertyCopiesCollectionAnalyzerUnitTest
{
    /// <summary>Verifies an expression-bodied property ending in ToArray is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedToArrayIsReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ToListIsReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ToHashSetIsReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ToDictionaryIsReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ArrayCloneIsReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                private readonly int[] _values = new int[4];

                public int[] {|PSH1017:Values|} => (int[])_values.Clone();
            }
            """);

    /// <summary>Verifies a collection seeded from a field through its constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeedingConstructorIsReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task InlineArrayIsReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task BlockBodiedGetterIsReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task BranchedReturnIsReportedAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task AutoPropertyIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public int[] Items { get; } = new int[4];
            }
            """);

    /// <summary>Verifies a property that hands back the field itself stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CachedFieldIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task CachedCopyIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ImmutableSnapshotFieldIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task IndexerIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task SetAccessorIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task CapacityConstructorIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public List<int> Scratch => new List<int>(8);
            }
            """);

    /// <summary>Verifies a read-only wrapper stays clean; it wraps the list instead of copying it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyWrapperIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task CopyInsideLambdaIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task ExcludedPropertyIsCleanAsync()
        => await VerifyWithConfigAsync(
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
    [Test]
    public async Task UnrelatedExclusionStillReportsAsync()
        => await VerifyWithConfigAsync(
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

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyPropertyCopy.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer verification with one editorconfig setting applied.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="setting">The editorconfig line to apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyWithConfigAsync(string source, string setting)
    {
        var test = new VerifyPropertyCopy.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", $"""
            root = true
            [*.cs]
            {setting}

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
