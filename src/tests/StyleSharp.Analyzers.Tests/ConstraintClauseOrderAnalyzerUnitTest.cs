// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;
using VerifyConstraintOrder = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1221ConstraintClauseOrderAnalyzer,
    StyleSharp.Analyzers.Sst1221ConstraintClauseOrderCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the constraint-clause order rule (SST1221) and its reorder fix.</summary>
public class ConstraintClauseOrderAnalyzerUnitTest
{
    /// <summary>Verifies missing, single, and unbound constraint clauses decline individual and batch edits.</summary>
    /// <param name="source">The declaration at the stale diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }")]
    [Arguments("class C<T> where T : class { }")]
    [Arguments("class C<T> where U : class where T : class { }")]
    [Arguments("class C where U : class where T : class { }")]
    public async Task UnsortableConstraintsHaveNoRewriteAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        SyntaxNode target = root.DescendantNodes().OfType<TypeParameterConstraintClauseSyntax>().FirstOrDefault() ?? (SyntaxNode)root;
        var diagnostic = Diagnostic.Create(OrderingRules.ConstraintClauseOrder, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1221ConstraintClauseOrderCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies incomplete constraint lists retain the trivia attached to their colon when reordered.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyConstraintListsKeepSlotTriviaAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", "class C<T, U> where U : class where T : class { }");
        var root = (await document.GetSyntaxRootAsync())!;
        var clauses = root.DescendantNodes().OfType<TypeParameterConstraintClauseSyntax>().ToArray();
        root = root.ReplaceNodes(clauses, static (original, _) => original.WithConstraints(default).WithColonToken(original.ColonToken.WithTrailingTrivia(SyntaxFactory.Space)));
        document = document.WithSyntaxRoot(root);
        root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(OrderingRules.ConstraintClauseOrder, root.DescendantNodes().OfType<TypeParameterConstraintClauseSyntax>().Last().GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1221ConstraintClauseOrderCodeFixProvider>().CreateContainer();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)container.GetExport<CodeFixProvider>()).RegisterBatchEdits(editor, diagnostic);
        var reordered = editor.GetChangedRoot().DescendantNodes().OfType<TypeParameterConstraintClauseSyntax>().ToArray();
        await Assert.That(reordered.Select(clause => clause.Name.Identifier.ValueText).ToArray()).IsEquivalentTo(new[] { "T", "U" });
        await Assert.That(reordered[0].Name.Identifier.ValueText).IsEqualTo("T");
        await Assert.That(reordered.All(clause => clause.Constraints.Count == 0 && clause.ColonToken.TrailingTrivia.ToFullString() == " ")).IsTrue();
    }

    /// <summary>Verifies out-of-order type constraint clauses are reported and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReorderedTypeConstraintsReportedAsync()
    {
        const string Source = """
                              public class C<TKey, TValue>
                                  where TValue : class
                                  {|SST1221:where TKey : new()|}
                              {
                              }
                              """;
        const string FixedSource = """
                                   public class C<TKey, TValue>
                                       where TKey : new()
                                       where TValue : class
                                   {
                                   }
                                   """;
        await VerifyConstraintOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies out-of-order method constraint clauses are reported and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReorderedMethodConstraintsReportedAsync()
    {
        const string Source = """
                              public class Holder
                              {
                                  public static void M<TA, TB>()
                                      where TB : class
                                      {|SST1221:where TA : new()|}
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class Holder
                                   {
                                       public static void M<TA, TB>()
                                           where TA : new()
                                           where TB : class
                                       {
                                       }
                                   }
                                   """;
        await VerifyConstraintOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies constraint clauses already in type-parameter order are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InOrderConstraintsAreCleanAsync() =>
        VerifyConstraintOrder.VerifyAnalyzerAsync(
            """
            public class C<TKey, TValue>
                where TKey : new()
                where TValue : class
            {
            }
            """);

    /// <summary>Verifies a single constraint clause is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleConstraintIsCleanAsync() =>
        VerifyConstraintOrder.VerifyAnalyzerAsync(
            """
            public class C<TKey, TValue>
                where TValue : class
            {
            }
            """);
}
