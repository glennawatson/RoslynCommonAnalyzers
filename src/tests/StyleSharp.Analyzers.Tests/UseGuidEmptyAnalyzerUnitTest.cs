// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using VerifyGuid = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2012UseGuidEmptyAnalyzer,
    StyleSharp.Analyzers.Sst2012UseGuidEmptyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2012 (use Guid.Empty for the empty GUID) and its fix.</summary>
public class UseGuidEmptyAnalyzerUnitTest
{
    /// <summary>The diagnostic accepted by the GUID code fix.</summary>
    private const string DiagnosticId = "SST2012";

    /// <summary>The source document used by direct code-fix tests.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>The core library exposed under an alias as well as the global namespace.</summary>
    private static readonly ImmutableArray<MetadataReference> AliasedReferences =
        [RuntimeMetadataReferences.CoreLibrary.WithAliases(["global", "GuidAssembly"])];

    /// <summary>Verifies directly declared, nullable, and contextual target types resolve to the framework GUID.</summary>
    /// <param name="source">A construction whose inferred value is an empty GUID.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("using System; class C { Guid Value { get; } = {|SST2012:new()|}; }")]
    [Arguments("using G = System.Guid; class C { G value = {|SST2012:new()|}; }")]
    [Arguments("using G = System.Guid?; class C { G value = {|SST2012:new()|}; }")]
    [Arguments("using System; class C { Guid? value = {|SST2012:new()|}; }")]
    [Arguments("using System; class C { Nullable<Guid> value = {|SST2012:new()|}; }")]
    [Arguments("using System; class C { System.Nullable<Guid> value = {|SST2012:new()|}; }")]
    [Arguments("using S = System; class C { object M() => {|SST2012:new S::Guid()|}; }")]
    [Arguments("using System; class C { void Accept(Guid value) { } void M() { Accept({|SST2012:new()|}); } }")]
    [Arguments("using System; class C { void M(Guid value = {|SST2012:new()|}) { } }")]
    public Task TargetTypeShapesIdentifyEmptyGuidsAsync(string source) => VerifyGuid.VerifyAnalyzerAsync(source);

    /// <summary>Verifies syntax and type near-misses do not name the framework GUID constructor.</summary>
    /// <param name="source">A construction excluded by the rule.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("using System; class C { Guid value = new Guid { }; }")]
    [Arguments("using System; class C { Guid value = new Guid() { }; }")]
    [Arguments("using System; class C { Guid value = new() { }; }")]
    [Arguments("using G = System.Guid; class C { G value = new G(); }")]
    [Arguments("class C { int value = new int(); }")]
    [Arguments("class C { int value = new(); }")]
    [Arguments("class C { (int, int) value = new(); }")]
    [Arguments("class Guid<T> { } class C { Guid<int> value = new(); }")]
    [Arguments("namespace N { class Guid<T> { } } class C { N.Guid<int> value = new(); }")]
    [Arguments("class Guid<T> { } class C { Guid<int> value = new Guid<int>(); }")]
    [Arguments("class Guid { } class C { Guid value = new global::Guid(); }")]
    public Task NonGuidAndInitializedConstructionsAreCleanAsync(string source) => TargetTypeShapesIdentifyEmptyGuidsAsync(source);

    /// <summary>Verifies unresolved and generic targets cannot identify a concrete GUID construction.</summary>
    /// <param name="source">The unresolved or generic construction under analysis.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class C { object M() => new Guid(); }")]
    [Arguments("class C { void M() { var value = new(); } }")]
    [Arguments("class C<T> where T : new() { T value = new(); }")]
    public Task UnresolvedAndGenericConstructionTypesAreCleanAsync(string source) =>
        new VerifyGuid.Test { TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a same-named source type is ignored when no framework GUID metadata exists.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingFrameworkGuidLeavesLookalikeConstructionAloneAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class Guid { } class C { Guid value = new Guid(); }");
        var compilation = CSharpCompilation.Create("MissingGuid", [tree], options: new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Sst2012UseGuidEmptyAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies target-typed construction falls back when Guid is not in scope.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TargetTypedConstructionWithoutUsingIsQualifiedAsync() =>
        VerifyGuid.VerifyCodeFixAsync(
            "class C { System.Guid M() => {|SST2012:new()|}; }",
            "class C { System.Guid M() => global::System.Guid.Empty; }");

    /// <summary>Verifies an unrelated Guid.Empty member cannot replace a framework GUID.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ShadowingGuidNameUsesFrameworkFieldAsync() =>
        VerifyGuid.VerifyCodeFixAsync(
            "class Guid { public static int Empty; } class C { System.Guid M() => {|SST2012:new()|}; }",
            "class Guid { public static int Empty; } class C { System.Guid M() => global::System.Guid.Empty; }");

    /// <summary>Verifies registration and batch editing reject stale spans and unusable Empty members.</summary>
    /// <param name="expression">The diagnostic's selected expression.</param>
    /// <param name="members">The replacement framework type's members.</param>
    /// <param name="expected">The replacement, or null if the fix is unavailable.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("1", "public static Guid Empty;", null)]
    [Arguments("new System.Guid()", "", null)]
    [Arguments("new System.Guid()", "public Guid Empty;", null)]
    [Arguments("new System.Guid()", "public static Guid Empty => default;", null)]
    [Arguments("new System.Guid()", "private static Guid Empty;", null)]
    [Arguments("new System.Guid()", "public static Guid Empty;", "System.Guid.Empty")]
    public async Task ReplacementRequiresAnAccessibleStaticFieldAsync(string expression, string members, string? expected)
    {
        var source = $"namespace System {{ public struct Guid {{ {members} }} }} class C {{ object M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("GuidFix", LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = (await document.GetSemanticModelAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, expression);
        using var container = new ContainerConfiguration().WithPart<Sst2012UseGuidEmptyCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(expected is null ? 0 : 1);
        var edit = Sst2012UseGuidEmptyCodeFixProvider.TryRewrite(root, model, diagnostic);
        await Assert.That(edit is not null).IsEqualTo(expected is not null);
        await Assert.That(edit?.Replacement.ToString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2012UseGuidEmptyCodeFixProvider>(editor, diagnostic);
        var expectedSource = $"namespace System {{ public struct Guid {{ {members} }} }} class C {{ object M() => {expected ?? expression}; }}";
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expectedSource);
    }

    /// <summary>Verifies an alias can resolve Empty when a source type shadows the System namespace.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AliasedGuidWorksWhenGlobalNamespaceIsShadowedAsync()
    {
        const string Source = "extern alias GuidAssembly; using G = GuidAssembly::System.Guid; class System { } class C { G M() => new G(); }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("AliasedGuidFix", LanguageNames.CSharp)
            .WithMetadataReferences(AliasedReferences).AddDocument(DocumentName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, "new G()");
        var actions = new List<CodeAction>();
        using var container = new ContainerConfiguration().WithPart<Sst2012UseGuidEmptyCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo("extern alias GuidAssembly; using G = GuidAssembly::System.Guid; class System { } class C { G M() => G.Empty; }");
    }

    /// <summary>Verifies a missing framework type makes both registration and batch editing unavailable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingGuidTypeOffersNoFixAsync()
    {
        const string Source = "class C { object M() => new Guid(); }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("MissingGuidFix", LanguageNames.CSharp).AddDocument(DocumentName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = LanguageStyleCodeFixProviderTests.CreateDiagnostic(root, DiagnosticId, "new Guid()");
        var actions = new List<CodeAction>();
        using var container = new ContainerConfiguration().WithPart<Sst2012UseGuidEmptyCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2012UseGuidEmptyCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }

    /// <summary>Verifies the parameterless construction is reported and replaced by the value it produces.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterlessConstructionIsReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private Guid _id = {|SST2012:new Guid()|};

                                  public Guid Blank() => {|SST2012:new Guid()|};

                                  public bool IsBlank(Guid id) => id == {|SST2012:new Guid()|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private Guid _id = Guid.Empty;

                                       public Guid Blank() => Guid.Empty;

                                       public bool IsBlank(Guid id) => id == Guid.Empty;
                                   }
                                   """;

        await VerifyGuid.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the replacement is spelled the way the construction was.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedConstructionKeepsItsQualificationAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private System.Guid _id = {|SST2012:new System.Guid()|};

                                  public System.Guid Read() => _id;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private System.Guid _id = System.Guid.Empty;

                                       public System.Guid Read() => _id;
                                   }
                                   """;

        await VerifyGuid.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a target-typed construction is reported and given a name for the value.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TargetTypedConstructionIsReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private Guid _id = {|SST2012:new()|};

                                  public Guid Read() => _id;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private Guid _id = Guid.Empty;

                                       public Guid Read() => _id;
                                   }
                                   """;

        await VerifyGuid.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a generated GUID and a seeded one are left alone: they mean what they say.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GeneratedAndSeededGuidsAreCleanAsync() =>
        VerifyGuid.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                private Guid _fresh = Guid.NewGuid();

                private Guid _seeded = new Guid("00000000-0000-0000-0000-000000000001");

                private Guid _named = Guid.Empty;

                public Guid Fresh() => _fresh;

                public Guid Seeded() => _seeded;

                public Guid Named() => _named;
            }
            """);

    /// <summary>Verifies a parameterless construction of another type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstructionOfAnotherTypeIsCleanAsync() =>
        VerifyGuid.VerifyAnalyzerAsync(
            """
            namespace Fakes
            {
                public struct Guid
                {
                }
            }

            public class C
            {
                private Fakes.Guid _id = new Fakes.Guid();

                public Fakes.Guid Read() => _id;
            }
            """);
}
