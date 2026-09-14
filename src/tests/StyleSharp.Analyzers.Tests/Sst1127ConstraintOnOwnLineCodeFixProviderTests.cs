// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests refusal to move constraints when a diagnostic is stale or a comment intervenes.</summary>
public class Sst1127ConstraintOnOwnLineCodeFixProviderTests
{
    /// <summary>Verifies an unattached clause without a preceding declaration token cannot produce a text edit.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DetachedConstraintIsIgnoredAsync()
    {
        var parsed = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseCompilationUnit("class C<T> where T : class { }");
        var clause = parsed.DescendantNodes().OfType<TypeParameterConstraintClauseSyntax>().Single().WithoutTrivia();
        var diagnostic = Diagnostic.Create(ReadabilityRules.ConstraintOnOwnLine, clause.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1127ConstraintOnOwnLineCodeFixProvider>().CreateContainer();
        var changes = new List<TextChange>();
        ((ITextChangeBatchableCodeFix)container.GetExport<CodeFixProvider>()).RegisterTextChanges(SourceText.From(clause.ToFullString()), clause, diagnostic, changes);
        await Assert.That(changes).IsEmpty();
    }

    /// <summary>Verifies registration and batch edits leave non-whitespace and unrelated syntax unchanged.</summary>
    /// <param name="source">The source containing the diagnostic.</param>
    /// <param name="onClause">Whether the diagnostic points to a constraint.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C<T> /* keep */ where T : class { }", true)]
    [Arguments("class C<T> where T : class { }", false)]
    public async Task UnsafeConstraintEditsAreIgnoredAsync(string source, bool onClause)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = onClause ? (SyntaxNode)root.DescendantNodes().OfType<TypeParameterConstraintClauseSyntax>().Single() : root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(ReadabilityRules.ConstraintOnOwnLine, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1127ConstraintOnOwnLineCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var changes = new List<TextChange>();
        ((ITextChangeBatchableCodeFix)provider).RegisterTextChanges(await document.GetTextAsync(), root, diagnostic, changes);
        await Assert.That(changes).IsEmpty();
    }
}
