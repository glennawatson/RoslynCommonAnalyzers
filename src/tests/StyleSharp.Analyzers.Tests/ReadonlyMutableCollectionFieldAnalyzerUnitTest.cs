// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using VerifyReadonlyMutableCollectionField = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2322ReadonlyMutableCollectionFieldAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2322ReadonlyMutableCollectionFieldAnalyzer"/>.</summary>
public class ReadonlyMutableCollectionFieldAnalyzerUnitTest
{
    /// <summary>Verifies a public readonly field of a mutable collection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PublicReadonlyListFieldIsReportedAsync() =>
        VerifyReadonlyMutableCollectionField.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public readonly List<int> {|SST2322:Items|} = new();
            }
            """);

    /// <summary>Verifies an internal readonly field of a mutable collection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InternalReadonlyListFieldIsReportedAsync() =>
        VerifyReadonlyMutableCollectionField.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                internal readonly List<int> {|SST2322:Items|} = new();
            }
            """);

    /// <summary>Verifies a protected readonly field of a mutable collection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ProtectedReadonlyListFieldIsReportedAsync() =>
        VerifyReadonlyMutableCollectionField.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                protected readonly List<int> {|SST2322:Items|} = new();
            }
            """);

    /// <summary>Verifies a protected internal readonly field of a mutable collection is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ProtectedInternalReadonlyListFieldIsReportedAsync() =>
        VerifyReadonlyMutableCollectionField.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                protected internal readonly List<int> {|SST2322:Items|} = new();
            }
            """);

    /// <summary>Verifies every mutable collection shape — array, dictionary, hash set, and object-model collection — is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryMutableCollectionShapeIsReportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Collections.ObjectModel;

                              public class C
                              {
                                  public readonly int[] {|SST2322:Values|} = new int[3];

                                  public readonly Dictionary<string, int> {|SST2322:Map|} = new();

                                  public readonly HashSet<string> {|SST2322:Names|} = new();

                                  public readonly Collection<int> {|SST2322:Items|} = new();

                                  public readonly IList<int> {|SST2322:Exposed|} = new List<int>();
                              }
                              """;
        var test = new VerifyReadonlyMutableCollectionField.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies every declarator of one declaration is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EveryDeclaratorOfADeclarationIsReportedAsync() =>
        VerifyReadonlyMutableCollectionField.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public readonly List<int> {|SST2322:First|} = new(), {|SST2322:Second|} = new();
            }
            """);

    /// <summary>Verifies a private readonly field keeps the collection under the type's own control and is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateReadonlyListFieldIsCleanAsync() =>
        VerifyReadonlyMutableCollectionField.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                private readonly List<int> _items = new();

                public int Count => _items.Count;
            }
            """);

    /// <summary>Verifies a field with no accessibility keyword, which is private, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImplicitlyPrivateReadonlyListFieldIsCleanAsync() =>
        VerifyReadonlyMutableCollectionField.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                readonly List<int> _items = new();

                public int Count => _items.Count;
            }
            """);

    /// <summary>Verifies a static readonly collection field is out of scope here.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticReadonlyListFieldIsCleanAsync() =>
        VerifyReadonlyMutableCollectionField.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public static readonly List<int> Items = new();
            }
            """);

    /// <summary>Verifies a readonly field of a non-collection type is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReadonlyScalarFieldIsCleanAsync() =>
        VerifyReadonlyMutableCollectionField.VerifyAnalyzerAsync(
            """
            public class C
            {
                public readonly int Limit = 3;

                public readonly string Name = "n";
            }
            """);

    /// <summary>Verifies a readonly field exposed through a read-only collection type is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadonlyReadOnlyViewFieldIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Collections.ObjectModel;

                              public class C
                              {
                                  public readonly IReadOnlyList<int> Exposed = new List<int>();

                                  public readonly ReadOnlyCollection<int> Wrapped = new(new List<int>());
                              }
                              """;
        var test = new VerifyReadonlyMutableCollectionField.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a non-readonly collection field is left to other rules.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The reference itself can move, which is a plainer problem than the one this rule guards.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonReadonlyListFieldIsCleanAsync() =>
        VerifyReadonlyMutableCollectionField.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public List<int> Items = new();
            }
            """);
}
