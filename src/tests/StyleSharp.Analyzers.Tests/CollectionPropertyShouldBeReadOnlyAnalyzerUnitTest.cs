// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCollectionProperty = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2305CollectionPropertyShouldBeReadOnlyAnalyzer,
    StyleSharp.Analyzers.Sst2305CollectionPropertyShouldBeReadOnlyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2305 (collection properties should not be settable) and its fix.</summary>
public class CollectionPropertyShouldBeReadOnlyAnalyzerUnitTest
{
    /// <summary>The <c>init</c>-accessor polyfill records need on the test reference assemblies.</summary>
    private const string IsExternalInit = """

        namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
        """;

    /// <summary>Verifies a settable list property is reported and the setter removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SettableListIsFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public List<int> {|SST2305:Items|} { get; set; } = new List<int>();
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public List<int> Items { get; } = new List<int>();
                                   }
                                   """;
        await VerifyCollectionProperty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix removes a block-bodied setter and leaves the getter's layout alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockBodiedSetterIsRemovedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  private List<int> _items = new List<int>();

                                  public List<int> {|SST2305:Items|}
                                  {
                                      get => _items;
                                      set => _items = value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       private List<int> _items = new List<int>();

                                       public List<int> Items
                                       {
                                           get => _items;
                                       }
                                   }
                                   """;
        await VerifyCollectionProperty.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies every mutable collection shape the rule recognizes is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryMutableCollectionShapeIsReportedAsync()
        => await VerifyCollectionProperty.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Collections.ObjectModel;

            public sealed class C
            {
                public int[] {|SST2305:Values|} { get; set; }

                public Dictionary<string, int> {|SST2305:Map|} { get; set; }

                public HashSet<int> {|SST2305:Set|} { get; set; }

                public Collection<int> {|SST2305:Bag|} { get; set; }

                public ICollection<int> {|SST2305:Collected|} { get; set; }

                public IList<int> {|SST2305:Listed|} { get; set; }
            }
            """);

    /// <summary>Verifies a property whose type cannot be mutated through the reference is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A read-only view, a read-only interface, a scalar, and a string are all silent.</remarks>
    [Test]
    public async Task NonMutableTypesAreCleanAsync()
        => await VerifyCollectionProperty.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Collections.ObjectModel;

            public sealed class C
            {
                public IReadOnlyList<int> Readable { get; set; }

                public IEnumerable<int> Sequence { get; set; }

                public ReadOnlyCollection<int> View { get; set; }

                public string Text { get; set; }

                public int Count { get; set; }
            }
            """);

    /// <summary>Verifies an immutable collection is a value, so replacing it is an assignment like any other.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImmutableCollectionsAreCleanAsync()
    {
        const string Source = """
                              using System.Collections.Immutable;

                              public sealed class C
                              {
                                  public ImmutableArray<int> Values { get; set; }

                                  public ImmutableList<int> Items { get; set; }
                              }
                              """;
        var test = new VerifyCollectionProperty.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the accessors the rule already asks for are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A get-only property is the fix; an <c>init</c> setter builds the object once; a private setter keeps the collection under the type's control.</remarks>
    [Test]
    public async Task SettledAccessorsAreCleanAsync()
        => await VerifyCollectionProperty.VerifyAnalyzerAsync(
            $$"""
            using System.Collections.Generic;

            public sealed class C
            {
                public List<int> GetOnly { get; } = new List<int>();

                public List<int> Built { get; init; }

                public List<int> Owned { get; private set; }

                private List<int> Hidden { get; set; }
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a required property keeps the setter an object initializer has to satisfy.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RequiredPropertyIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public required List<int> Items { get; set; }
                              }
                              """;
        var test = new VerifyCollectionProperty.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an attribute on the property, or on its type, is read as a contract that needs the setter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AttributedDeclarationsAreCleanAsync()
        => await VerifyCollectionProperty.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
            public sealed class JsonPropertyNameAttribute : Attribute
            {
                public JsonPropertyNameAttribute(string name) => Name = name;

                public string Name { get; }
            }

            public sealed class Payload
            {
                [JsonPropertyName("items")]
                public List<int> Items { get; set; }
            }

            [JsonPropertyName("record")]
            public sealed class Record
            {
                public List<int> Items { get; set; }
            }

            [Serializable]
            public sealed class Legacy
            {
                public List<int> Items { get; set; }
            }
            """);

    /// <summary>Verifies a property whose shape an interface or a base type dictates is reported at that declaration instead.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritedShapesAreReportedAtTheirSourceAsync()
        => await VerifyCollectionProperty.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public interface IStore
            {
                List<int> {|SST2305:Items|} { get; set; }
            }

            public abstract class Base
            {
                public abstract List<int> {|SST2305:Values|} { get; set; }
            }

            public sealed class Store : Base, IStore
            {
                public List<int> Items { get; set; }

                public override List<int> Values { get; set; }
            }

            public sealed class Explicit : IStore
            {
                List<int> IStore.Items { get; set; }
            }
            """);

    /// <summary>Verifies a positional record's members are the constructor's, not a settable property.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PositionalRecordIsCleanAsync()
        => await VerifyCollectionProperty.VerifyAnalyzerAsync(
            $$"""
            using System.Collections.Generic;

            public record Basket(List<int> Items);{{IsExternalInit}}
            """);

    /// <summary>Verifies a property declared in a record body is measured like any other.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeclaredRecordPropertyIsReportedAsync()
        => await VerifyCollectionProperty.VerifyAnalyzerAsync(
            $$"""
            using System.Collections.Generic;

            public record Basket
            {
                public List<int> {|SST2305:Items|} { get; set; }
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a static settable collection is reported like an instance one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticPropertyIsReportedAsync()
        => await VerifyCollectionProperty.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public static class Registry
            {
                public static List<int> {|SST2305:Items|} { get; set; }
            }
            """);
}
