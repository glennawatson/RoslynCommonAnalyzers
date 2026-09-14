// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests frozen lookup edits for stale diagnostics, constructor arguments, and namespace imports.</summary>
public sealed class Psh1114FreezeStaticLookupsCodeFixProviderTests
{
    /// <summary>The document containing the reported field.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>The field selected when malformed syntax adds other declarations.</summary>
    private const string LookupFieldName = "Lookup";

    /// <summary>Verifies unsupported declarations remain unchanged through registration and batch editing.</summary>
    /// <param name="source">The source carrying a stale lookup diagnostic.</param>
    /// <param name="reportClass">Whether the diagnostic selects the class instead of its variable.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }", true)]
    [Arguments("class C { void M() { HashSet<int> Lookup = new HashSet<int>(); } }", false)]
    [Arguments("class C { System.Collections.Generic.HashSet<int> Lookup = new System.Collections.Generic.HashSet<int>(); }", false)]
    [Arguments("class C { object Lookup = new object(); }", false)]
    [Arguments("class C { HashSet<int> Lookup; }", false)]
    [Arguments("class C { HashSet<int> Lookup = null; }", false)]
    [Arguments("class C { HashSet<int> Lookup = Create(); }", false)]
    [Arguments("class C { HashSet<int> Lookup = []; }", false)]
    [Arguments("class C { List<int> Lookup = new List<int>(); }", false)]
    public async Task UnsupportedDeclarationHasNoFixAsync(string source, bool reportClass)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        SyntaxNode target = reportClass
            ? root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single()
            : root.DescendantNodes().OfType<VariableDeclaratorSyntax>().Single();
        var diagnostic = Diagnostic.Create(CollectionRules.FreezeStaticLookups, target.GetLocation(), "Set", LookupFieldName);
        using var container = new ContainerConfiguration().WithPart<Psh1114FreezeStaticLookupsCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1114FreezeStaticLookupsCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies constructor binding preserves comparers and ignores capacity arguments.</summary>
    /// <param name="type">The original lookup type.</param>
    /// <param name="initializer">The original creation expression.</param>
    /// <param name="replacementType">The frozen field type.</param>
    /// <param name="replacementInitializer">The expected fluent wrapper.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Dictionary<string, int>", "new Dictionary<string, int>(3)", "FrozenDictionary<string, int>", "new Dictionary<string, int>(3).ToFrozenDictionary()")]
    [Arguments(
        "Dictionary<string, int>",
        "new Dictionary<string, int>(3, StringComparer.Ordinal)",
        "FrozenDictionary<string, int>",
        "new Dictionary<string, int>(3, StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal)")]
    [Arguments(
        "Dictionary<string, int>",
        "new Dictionary<string, int>(capacity: 3, comparer: StringComparer.Ordinal)",
        "FrozenDictionary<string, int>",
        "new Dictionary<string, int>(capacity: 3, comparer: StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal)")]
    [Arguments(
        "Dictionary<string, int>",
        "new Dictionary<string, int>(comparer: StringComparer.Ordinal, capacity: 3)",
        "FrozenDictionary<string, int>",
        "new Dictionary<string, int>(comparer: StringComparer.Ordinal, capacity: 3).ToFrozenDictionary(StringComparer.Ordinal)")]
    [Arguments("Dictionary<string, int>", "new Dictionary<string, int>(capacity: 3)", "FrozenDictionary<string, int>", "new Dictionary<string, int>(capacity: 3).ToFrozenDictionary()")]
    [Arguments(
        "HashSet<string>",
        "new HashSet<string>(comparer: StringComparer.Ordinal)",
        "FrozenSet<string>",
        "new HashSet<string>(comparer: StringComparer.Ordinal).ToFrozenSet(StringComparer.Ordinal)")]
    [Arguments("HashSet<string>", "new HashSet<string>(capacity: 3)", "FrozenSet<string>", "new HashSet<string>(capacity: 3).ToFrozenSet()")]
    [Arguments("HashSet<string>", "new HashSet<string>(unknown: StringComparer.Ordinal)", "FrozenSet<string>", "new HashSet<string>(unknown: StringComparer.Ordinal).ToFrozenSet()")]
    public async Task ConstructorArgumentsControlComparerAsync(string type, string initializer, string replacementType, string replacementInitializer)
    {
        var source = $$"""
            using System;
            using System.Collections.Frozen;
            using System.Collections.Generic;
            class C { private static readonly {{type}} Lookup = {{initializer}}; }
            """;
        var replacement = $"private static readonly {replacementType} Lookup = {replacementInitializer};";
        await VerifyRewriteAsync(source, replacement);
    }

    /// <summary>Verifies imports that do not expose extension methods use fully qualified static calls.</summary>
    /// <param name="import">The using directive available to the field.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("using Frozen = System.Collections.Frozen;")]
    [Arguments("global using System.Collections.Frozen;")]
    [Arguments("using static System.Collections.Frozen.FrozenSet;")]
    public async Task NonLocalFrozenImportsUseQualifiedCallsAsync(string import)
    {
        var source = $$"""
            {{import}}
            using System;
            using System.Collections.Generic;
            class C { private static readonly HashSet<string> Lookup = new(StringComparer.Ordinal); }
            """;
        const string Replacement = "private static readonly global::System.Collections.Frozen.FrozenSet<string> Lookup = "
            + "global::System.Collections.Frozen.FrozenSet.ToFrozenSet(new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);";
        await VerifyRewriteAsync(source, Replacement);
    }

    /// <summary>Records that a malformed import prevents comparer binding, so the existing fix forwards no comparer.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MalformedImportLeavesComparerUnresolvedAsync() =>
        VerifyRewriteAsync(
            """
            using int;
            using System;
            using System.Collections.Generic;
            class C { private static readonly HashSet<string> Lookup = new(StringComparer.Ordinal); }
            """,
            "private static readonly global::System.Collections.Frozen.FrozenSet<string> Lookup = "
            + "global::System.Collections.Frozen.FrozenSet.ToFrozenSet(new HashSet<string>(StringComparer.Ordinal));");

    /// <summary>Verifies syntax-compatible constructors with expanded arguments are handled without indexing beyond parameters.</summary>
    /// <param name="hasImport">Whether the frozen namespace is imported.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ExpandedConstructorArgumentsDoNotBecomeComparersAsync(bool hasImport)
    {
        var import = hasImport ? "using System.Collections.Frozen;" : string.Empty;
        var source = $$"""
            {{import}}
            class HashSet<T> { public HashSet(params int[] values) { } }
            class C { private static readonly HashSet<int> Lookup = new HashSet<int>(1, 2); }
            """;
        var replacement = hasImport
            ? "private static readonly FrozenSet<int> Lookup = new HashSet<int>(1, 2).ToFrozenSet();"
            : "private static readonly global::System.Collections.Frozen.FrozenSet<int> Lookup = global::System.Collections.Frozen.FrozenSet.ToFrozenSet(new HashSet<int>(1, 2));";
        await VerifyRewriteAsync(source, replacement);
    }

    /// <summary>Checks that individual and batch edits produce the same expected field.</summary>
    /// <param name="source">The source containing exactly one lookup field.</param>
    /// <param name="replacement">The expected field declaration.</param>
    /// <returns>A task representing the asynchronous verification.</returns>
    private static async Task VerifyRewriteAsync(string source, string replacement)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(DocumentName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var declarator = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Single(static variable => variable.Identifier.ValueText == LookupFieldName);
        var diagnostic = Diagnostic.Create(CollectionRules.FreezeStaticLookups, declarator.GetLocation(), "Set", LookupFieldName);
        using var container = new ContainerConfiguration().WithPart<Psh1114FreezeStaticLookupsCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var expected = SyntaxFactory.ParseMemberDeclaration(replacement)!.NormalizeWhitespace().ToFullString();
        await Assert.That(changedRoot.DescendantNodes().OfType<FieldDeclarationSyntax>().Single().NormalizeWhitespace().ToFullString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Psh1114FreezeStaticLookupsCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().DescendantNodes().OfType<FieldDeclarationSyntax>().Single().NormalizeWhitespace().ToFullString()).IsEqualTo(expected);
    }
}
