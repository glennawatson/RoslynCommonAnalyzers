// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EveryMutableCollectionShapeIsReportedAsync() =>
        VerifyCollectionProperty.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonMutableTypesAreCleanAsync() =>
        VerifyCollectionProperty.VerifyAnalyzerAsync(
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
        var test = new VerifyCollectionProperty.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the accessors the rule already asks for are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A get-only property is the fix; an <c>init</c> setter builds the object once; a private setter keeps the collection under the type's control.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SettledAccessorsAreCleanAsync() =>
        VerifyCollectionProperty.VerifyAnalyzerAsync(
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
        var test = new VerifyCollectionProperty.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an attribute on the property, or on its type, is read as a contract that needs the setter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AttributedDeclarationsAreCleanAsync() =>
        VerifyCollectionProperty.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InheritedShapesAreReportedAtTheirSourceAsync() =>
        VerifyCollectionProperty.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PositionalRecordIsCleanAsync() =>
        VerifyCollectionProperty.VerifyAnalyzerAsync(
            $$"""
            using System.Collections.Generic;

            public record Basket(List<int> Items);{{IsExternalInit}}
            """);

    /// <summary>Verifies a property declared in a record body is measured like any other.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DeclaredRecordPropertyIsReportedAsync() =>
        VerifyCollectionProperty.VerifyAnalyzerAsync(
            $$"""
            using System.Collections.Generic;

            public record Basket
            {
                public List<int> {|SST2305:Items|} { get; set; }
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a static settable collection is reported like an instance one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticPropertyIsReportedAsync() =>
        VerifyCollectionProperty.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public static class Registry
            {
                public static List<int> {|SST2305:Items|} { get; set; }
            }
            """);

    /// <summary>Verifies a property only its own type can reach keeps a setter that type uses.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PropertyAssignedInsideItsOwnTypeIsCleanAsync() =>
        VerifyCollectionProperty.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class Scanner
            {
                public void Run()
                {
                    var scan = default(ScanState);
                    scan.Seen = new List<int>();
                }

                private struct ScanState
                {
                    public List<int> Seen { get; set; }
                }
            }
            """);

    /// <summary>Verifies the reported nullable collection keeps its diagnostic when suppressed.</summary>
    /// <param name="suppress">Whether to apply the suppression from the report.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task IssueSnippetStillReportsAsync(bool suppress, CancellationToken cancellationToken)
    {
        const string Source = """
                              using System.Collections.ObjectModel;

                              public class C
                              {
                                  public Collection<C>? Owner { get; internal set; }
                              }
                              """;
        const string SuppressedSource = """
                                        using System.Collections.ObjectModel;
                                        using System.Diagnostics.CodeAnalysis;

                                        public class C
                                        {
                                            [SuppressMessage("Design", "SST2305", Justification = "Justification")]
                                            public Collection<C>? Owner { get; internal set; }
                                        }
                                        """;
        var diagnostics = await AnalyzeCollectionSourceAsync(suppress ? SuppressedSource : Source, cancellationToken);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST2305");
        await Assert.That(diagnostics[0].IsSuppressed).IsEqualTo(suppress);
        var text = await diagnostics[0].Location.SourceTree!.GetTextAsync(cancellationToken);
        await Assert.That(text.ToString(diagnostics[0].Location.SourceSpan)).IsEqualTo("Owner");
    }

    /// <summary>Verifies suppressions do not exempt a property, setter, or containing type.</summary>
    /// <param name="site">The declaration carrying the suppression.</param>
    /// <param name="name">The written suppression attribute name.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task SuppressionAtEachSiteStillReportsAsync(
        [Matrix("property", "setter", "type")] string site,
        [Matrix(
            "SuppressMessage",
            "SuppressMessageAttribute",
            "System.Diagnostics.CodeAnalysis.SuppressMessage",
            "global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute",
            "Diagnostics::SuppressMessage")] string name,
        CancellationToken cancellationToken)
    {
        var attribute = $"[{name}(\"Design\", \"SST2305\", Justification = \"Justification\")]";
        var source = $$"""
                       using System.Collections.ObjectModel;
                       using System.Diagnostics.CodeAnalysis;
                       using Diagnostics = System.Diagnostics.CodeAnalysis;

                       {{(site == "type" ? attribute : string.Empty)}}
                       public class C
                       {
                           {{(site == "property" ? attribute : string.Empty)}}
                           public Collection<C>? Owner { get; {{(site == "setter" ? attribute : string.Empty)}} internal set; }
                       }
                       """;
        var diagnostics = await AnalyzeCollectionSourceAsync(source, cancellationToken);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST2305");
    }

    /// <summary>Verifies tooling metadata is recognized without binding at all three exemption sites.</summary>
    /// <param name="name">The tooling attribute's short name.</param>
    /// <param name="suffix">The optional attribute suffix.</param>
    /// <param name="prefix">The optional namespace or alias qualification.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task ToolingAttributeNamesKeepSetterAsync(
        [Matrix(
            "SuppressMessage",
            "UnconditionalSuppressMessage",
            "ExcludeFromCodeCoverage",
            "DebuggerBrowsable",
            "DebuggerDisplay",
            "DebuggerHidden",
            "DebuggerNonUserCode",
            "DebuggerStepThrough",
            "DebuggerStepperBoundary",
            "DebuggerTypeProxy",
            "DebuggerVisualizer",
            "DebuggerDisableUserUnhandledExceptions",
            "EditorBrowsable",
            "CompilerGenerated")] string name,
        [Matrix("", "Attribute")] string suffix,
        [Matrix("", "Tools.", "global::Tools.", "Tools::")] string prefix)
    {
        var attribute = $"[{prefix}{name}{suffix}]";
        string[] sources =
        [
            $"class C {{ {attribute} public int[] Items {{ get; set; }} }}",
            $"class C {{ public int[] Items {{ get; {attribute} set; }} }}",
            $"{attribute} class C {{ public int[] Items {{ get; set; }} }}",
        ];
        foreach (var source in sources)
        {
            var declaration = (ClassDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0];
            var property = (PropertyDeclarationSyntax)declaration.Members[0];

            await Assert.That(Sst2305CollectionPropertyShouldBeReadOnlyAnalyzer.FindRemovableSetter(property)).IsNotNull();
        }
    }

    /// <summary>Verifies a contract still exempts each declaration alongside tooling metadata.</summary>
    /// <param name="attributes">The contract and optional tooling attributes in either order.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("[Contract]")]
    [Arguments("[ExcludeFromCodeCoverage, Contract]")]
    [Arguments("[Contract, ExcludeFromCodeCoverage]")]
    [Arguments("[ExcludeFromCodeCoverage][Contract]")]
    [Arguments("[Contract][ExcludeFromCodeCoverage]")]
    public async Task ContractAttributesStillExemptAsync(string attributes)
    {
        var source = $$"""
                       using System;
                       using System.Diagnostics.CodeAnalysis;

                       [AttributeUsage(AttributeTargets.All)]
                       public sealed class ContractAttribute : Attribute { }

                       public class PropertyContract
                       {
                           {{attributes}}
                           public int[] Items { get; set; }
                       }

                       public class SetterContract
                       {
                           public int[] Items { get; {{attributes}} set; }
                       }

                       {{attributes}}
                       public class TypeContract
                       {
                           public int[] Items { get; set; }
                       }
                       """;

        await VerifyCollectionProperty.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies real tooling attributes on all three declarations leave the report intact.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ToolingAttributesStillReportAsync()
    {
        const string Source = """
                              using System.ComponentModel;
                              using System.Diagnostics;
                              using System.Diagnostics.CodeAnalysis;
                              using System.Runtime.CompilerServices;

                              [CompilerGenerated]
                              [DebuggerDisplay("{Items}")]
                              public class C
                              {
                                  [EditorBrowsable(EditorBrowsableState.Never)]
                                  [DebuggerBrowsable(DebuggerBrowsableState.Never)]
                                  [ExcludeFromCodeCoverage]
                                  public int[] {|SST2305:Items|}
                                  {
                                      get;
                                      [DebuggerHidden]
                                      [DebuggerNonUserCode]
                                      [DebuggerStepThrough]
                                      [DebuggerStepperBoundary]
                                      set;
                                  }
                              }
                              """;

        await VerifyCollectionProperty.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Runs SST2305 with suppressed reports retained so a vanished trigger fails the test.</summary>
    /// <param name="source">The collection property source.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The reports, including diagnostics suppressed by attributes.</returns>
    private static async Task<System.Collections.Immutable.ImmutableArray<Diagnostic>> AnalyzeCollectionSourceAsync(string source, CancellationToken cancellationToken)
    {
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, cancellationToken);
        var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
        var compilation = CSharpCompilation.Create(
            "CollectionPropertySuppression",
            [tree],
            references,
            new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken))
        {
            await Assert.That(diagnostic.Severity).IsNotEqualTo(DiagnosticSeverity.Error);
        }

        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null,
            concurrentAnalysis: false,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: true);
        return await compilation.WithAnalyzers([new Sst2305CollectionPropertyShouldBeReadOnlyAnalyzer()], options).GetAnalyzerDiagnosticsAsync(cancellationToken);
    }
}
