// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using VerifyPartial = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2335PartialStaticMismatchAnalyzer,
    StyleSharp.Analyzers.Sst2335PartialStaticMismatchCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2335PartialStaticMismatchCodeFixProvider"/> (SST2335 add 'static').</summary>
public class Sst2335PartialStaticMismatchCodeFixUnitTest
{
    /// <summary>A two-part partial class where one part omits <c>static</c>.</summary>
    private const string TwoPartSource = """
        static partial class Widget
        {
        }

        partial class {|SST2335:Widget|}
        {
        }
        """;

    /// <summary>Both parts static after the fix.</summary>
    private const string TwoPartFixed = """
        static partial class Widget
        {
        }

        static partial class Widget
        {
        }
        """;

    /// <summary>A three-part partial class where two parts omit <c>static</c>.</summary>
    private const string ThreePartSource = """
        static partial class Gadget
        {
        }

        partial class {|SST2335:Gadget|}
        {
        }

        partial class {|SST2335:Gadget|}
        {
        }
        """;

    /// <summary>Every part static after Fix All.</summary>
    private const string ThreePartFixed = """
        static partial class Gadget
        {
        }

        static partial class Gadget
        {
        }

        static partial class Gadget
        {
        }
        """;

    /// <summary>Verifies both edit entry points retain trivia when modifiers changed after analysis.</summary>
    /// <param name="modifiers">The modifiers remaining on the declaration.</param>
    /// <param name="expectedModifiers">The modifiers after applying the fix.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "static ")]
    [Arguments("public ", "public static ")]
    [Arguments("partial ", "static partial ")]
    [Arguments("public partial ", "public static partial ")]
    public async Task ChangedModifiersPreserveTriviaAsync(string modifiers, string expectedModifiers)
    {
        var source = $"// Part\n{modifiers}class C {{ }}";
        var expected = $"// Part\n{expectedModifiers}class C {{ }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var declaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(DesignRules.PartialTypeStaticModifierMismatch, declaration.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2335PartialStaticMismatchCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies a diagnostic no longer inside a class registers neither action nor batch edit.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemovedClassIsIgnoredAsync()
    {
        const string Source = "struct C { }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(DesignRules.PartialTypeStaticModifierMismatch, root.DescendantNodes().OfType<StructDeclarationSyntax>().Single().Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2335PartialStaticMismatchCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(Source);
    }

    /// <summary>Verifies the fix adds <c>static</c> to the part that omits it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsStaticToOmittingPartAsync() =>
        VerifyPartial.VerifyCodeFixAsync(TwoPartSource, TwoPartFixed);

    /// <summary>Verifies Fix All makes every omitting part static.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixAllMakesEveryPartStaticAsync() =>
        VerifyPartial.VerifyCodeFixAsync(ThreePartSource, ThreePartFixed);
}
