// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using VerifyOwnsDisposable = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2315OwnsDisposableFieldAnalyzer,
    StyleSharp.Analyzers.Sst2315OwnsDisposableFieldCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2315 (a type owns a disposable but is not IDisposable).</summary>
public class Sst2315OwnsDisposableFieldAnalyzerUnitTest
{
    /// <summary>A custom disposable used by every case, so the framework's stream special-casing never applies.</summary>
    private const string Resource = """

        public sealed class Res : System.IDisposable
        {
            public static Res Create() => new Res();

            public string Name => string.Empty;

            public void Dispose()
            {
            }
        }
        """;

    /// <summary>A factory-owned disposable field to be fixed.</summary>
    private const string FactoryFieldSource = """
        public sealed class {|SST2315:C|}
        {
            private readonly Res _r = Res.Create();
        }
        """ + Resource;

    /// <summary>The type after the fix.</summary>
    private const string FactoryFieldFixed = """
        public sealed class C : System.IDisposable
        {
            private readonly Res _r = Res.Create();

            public void Dispose()
            {
                _r.Dispose();
            }
        }
        """ + Resource;

    /// <summary>Verifies a field assigned from a static factory is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FactoryAssignedFieldReportedAsync() =>
        VerifyOwnsDisposable.VerifyAnalyzerAsync(
            """
            public sealed class {|SST2315:C|}
            {
                private readonly Res _r = Res.Create();
            }
            """ + Resource);

    /// <summary>Verifies an auto-property initialized with new is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AutoPropertyNewReportedAsync() =>
        VerifyOwnsDisposable.VerifyAnalyzerAsync(
            """
            public sealed class {|SST2315:C|}
            {
                public Res R { get; } = new Res();
            }
            """ + Resource);

    /// <summary>Verifies a collection the type fills with new disposables is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CollectionOfDisposablesReportedAsync() =>
        VerifyOwnsDisposable.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class {|SST2315:C|}
            {
                private readonly List<Res> _all = new();

                public void Add() => _all.Add(new Res());
            }
            """ + Resource);

    /// <summary>Verifies an injected disposable is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InjectedFieldIsCleanAsync() =>
        VerifyOwnsDisposable.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private readonly Res _r;

                public C(Res r) => _r = r;
            }
            """ + Resource);

    /// <summary>Verifies a field constructed directly with new is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NewAssignedFieldIsCleanAsync() =>
        VerifyOwnsDisposable.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private readonly Res _r = new Res();
            }
            """ + Resource);

    /// <summary>Verifies a type that already implements IDisposable is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AlreadyDisposableIsCleanAsync() =>
        VerifyOwnsDisposable.VerifyAnalyzerAsync(
            """
            public sealed class C : System.IDisposable
            {
                private readonly Res _r = Res.Create();

                public void Dispose() => _r.Dispose();
            }
            """ + Resource);

    /// <summary>Verifies the fix adds IDisposable and a Dispose that releases the owned member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FactoryFieldFixedToDisposableAsync() =>
        VerifyOwnsDisposable.VerifyCodeFixAsync(FactoryFieldSource, FactoryFieldFixed);

    /// <summary>Verifies static, constant, injected and non-owning members remain clean.</summary>
    /// <param name="member">The members to inspect.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("static Res field = Res.Create();")]
    [Arguments("const string Name = null;")]
    [Arguments("static Res Value { get; } = new Res();")]
    [Arguments("Res field = new();")]
    [Arguments("Res Value => Res.Create();")]
    [Arguments("Res Value { get { return Res.Create(); } }")]
    [Arguments("Res Value { get => Res.Create(); }")]
    [Arguments("static Res injected = new Res(); Res Value { get; } = injected;")]
    [Arguments("static Factory factory = new Factory(); Res field = factory.Create(); class Factory { public Res Create() => new Res(); }")]
    [Arguments("System.Collections.Generic.List<int> values = new(); void Add() => values.Add(1);")]
    [Arguments("System.Collections.Generic.List<Res> values = new(); void Add(Res injected) => values.Add(injected);")]
    [Arguments("System.Collections.Generic.List<Res> values = new(); void Add() { System.GC.KeepAlive(values); values.Clear(); }")]
    [Arguments("System.Collections.Generic.List<Res> values = new(); void Add(System.Collections.Generic.List<Res> other) => other.Add(new Res());")]
    [Arguments("System.Collections.Generic.List<Res> values = new(); void Add() { Helper(); } static void Helper() { }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonOwningMembersAreCleanAsync(string member) =>
        VerifyOwnsDisposable.VerifyAnalyzerAsync($"class C {{ {member} }}{Resource}");

    /// <summary>Verifies non-owning members are skipped even after another member starts the ownership scan.</summary>
    /// <param name="member">A member preceding the owned resource.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Res injected;")]
    [Arguments("Res Injected { get; }")]
    [Arguments("static Res shared = Res.Create();")]
    [Arguments("const string Name = null;")]
    [Arguments("static Res Shared { get; } = new Res();")]
    [Arguments("Res Computed => Res.Create();")]
    [Arguments("int number = 1;")]
    [Arguments("static Res shared = Res.Create(); Res injected = shared;")]
    [Arguments("Res injected = null;")]
    [Arguments("Res Injected { get; } = null;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonOwningMembersDoNotHideOwnedResourceAsync(string member) =>
        VerifyOwnsDisposable.VerifyAnalyzerAsync($"class {{|SST2315:C|}} {{ {member} Res Owned {{ get; }} = new(); }}{Resource}");

    /// <summary>Verifies malformed computed properties and unresolved factories do not establish ownership.</summary>
    /// <param name="member">A malformed initializer or computed property.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Res Value => Res.Create() = new Res();")]
    [Arguments("Res Value { get { return Res.Create(); } } = new Res();")]
    [Arguments("Res Value { get => Res.Create(); } = new Res();")]
    [Arguments("Res field = Missing();")]
    public async Task UnresolvedOwnershipIsCleanAsync(string member)
    {
        var test = new VerifyOwnsDisposable.Test { TestCode = $"class C {{ {member} object candidate = new object(); }}{Resource}", CompilerDiagnostics = CompilerDiagnostics.None };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies ref structs are exempt despite a candidate owned resource.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RefStructOwnershipIsCleanAsync() =>
        VerifyOwnsDisposable.VerifyAnalyzerAsync($"ref struct C {{ Res field = Res.Create(); public C() {{ }} }}{Resource}");

    /// <summary>Verifies nullable concrete ownership is reported while constrained generic creation currently remains unreported.</summary>
    /// <param name="member">The owned generic or nullable auto-property.</param>
    /// <param name="typeName">The containing type name, marked when a diagnostic is expected.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("T Owned { get; } = new T();", "C")]
    [Arguments("Res? MaybeOwned { get; } = Res.Create();", "{|SST2315:C|}")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NullableAndGenericOwnershipFollowCurrentBehaviorAsync(string member, string typeName) =>
        VerifyOwnsDisposable.VerifyAnalyzerAsync($$"""
            #nullable enable
            class {{typeName}}<T> where T : class, System.IDisposable, new()
            {
                {{member}}
            }
            {{Resource}}
            """);

    /// <summary>Verifies async-only and mixed ownership cannot supply synchronous disposal fix metadata.</summary>
    /// <param name="members">The resources owned by the type.</param>
    /// <param name="expectedMembers">The synchronous disposal metadata.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Res first = Res.Create(), second = Res.Create();", "first,second")]
    [Arguments("AsyncRes Owned { get; } = new();", "")]
    [Arguments("Res first = Res.Create(); AsyncRes Owned { get; } = new();", "")]
    [Arguments("System.Collections.Generic.List<Res> values = new(); void Add() => values.Add(new());", "")]
    public async Task OwnershipDeterminesFixMetadataAsync(string members, string expectedMembers)
    {
        var source = $$"""
            class C { {{members}} }
            {{Resource}}
            class AsyncRes : System.IAsyncDisposable
            {
                public System.Threading.Tasks.ValueTask DisposeAsync() => default;
            }
            """;
        var compilation = CSharpCompilation.Create(
            "OwnershipMetadata",
            [CSharpSyntaxTree.ParseText(source)],
            RuntimeMetadataReferences.Platform,
            new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Sst2315OwnsDisposableFieldAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST2315");
        await Assert.That(diagnostics[0].Properties[Sst2315OwnsDisposableFieldAnalyzer.MembersToDisposeKey]).IsEqualTo(expectedMembers);
    }

    /// <summary>Verifies absent framework disposal or collection metadata does not crash ownership analysis.</summary>
    /// <param name="framework">The minimal disposal interface, if present.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("namespace System { public interface IDisposable { void Dispose(); } }")]
    public async Task MissingFrameworkTypesAreCleanAsync(string framework)
    {
        var compilation = CSharpCompilation.Create("MissingOwnershipFramework", [CSharpSyntaxTree.ParseText($"class C {{ object value = new object(); }}{framework}")]);
        var diagnostics = await compilation.WithAnalyzers([new Sst2315OwnsDisposableFieldAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a generated expression-bodied property with an initializer is not treated as auto-storage.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExpressionBodiedPropertyWithInitializerIsCleanAsync()
    {
        var root = await CSharpSyntaxTree.ParseText($"class C {{ Res Value => Res.Create(); object candidate = new object(); }}{Resource}").GetRootAsync();
        var property = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().First();
        var initializer = SyntaxFactory.EqualsValueClause(SyntaxFactory.ParseExpression("new Res()"));
        var changedRoot = root.ReplaceNode(property, property.WithInitializer(initializer));
        var compilation = CSharpCompilation.Create(
            "ComputedOwnership",
            [CSharpSyntaxTree.Create((CSharpSyntaxNode)changedRoot)],
            RuntimeMetadataReferences.Platform,
            new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Sst2315OwnsDisposableFieldAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }
}
