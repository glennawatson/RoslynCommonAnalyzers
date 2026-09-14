// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests readonly-field fix applicability and solution-wide reference classification.</summary>
public sealed class Sst1499MutableStaticFieldCodeFixProviderTests
{
    /// <summary>Verifies stale diagnostics and forbidden writes decline both individual and batch fixes.</summary>
    /// <param name="source">The declaration and its disqualifying use.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M() { } }")]
    [Arguments("class C { public const int Value = 1; }")]
    [Arguments("class C { public static readonly int Value; }")]
    [Arguments("class C { public static volatile int Value; }")]
    [Arguments("class C<T> where T : struct { public static T Value; }")]
    [Arguments("class C { public static int Value; C() { Value = 1; } }")]
    [Arguments("class C { public static int Value; static C() { void Set() { Value = 1; } Set(); } }")]
    [Arguments("class C { public static int Value; static void M() { ++Value; } }")]
    [Arguments("class C { public static int Value; static void M() { --Value; } }")]
    [Arguments("class C { public static int Value; static void M() { Value++; } }")]
    [Arguments("class C { public static int Value; static void M() { System.Threading.Interlocked.Increment(ref Value); } }")]
    [Arguments("class C { public static int Value; } class D { static D() { C.Value = 1; } }")]
    [Arguments("class C { public static int Value; static C() { System.Action set = () => Value = 1; set(); } }")]
    [Arguments("class C { public static int Value; static void M() { Value--; } }")]
    public async Task ForbiddenReadonlyRewriteHasNoFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument("Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var member = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First().Members.First();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.MutableStaticField, member.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1499MutableStaticFieldCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        await ((IAsyncBatchableCodeFix)provider).RegisterEditsAsync(editor, diagnostic, CancellationToken.None);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies reads, documentation references and static-constructor writes allow readonly on every declared variable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadsAndQualifiedInitializationAllowOneReadonlyModifierAsync()
    {
        const string Source = """
                              class C
                              {
                                  public static int First, Second;
                                  static C() { C.First = 1; Second = C.First; }
                                  /// <summary><see cref="First"/></summary>
                                  int M() => -C.First + Second;
                              }
                              """;
        const string Expected = """
                                class C
                                {
                                    public static readonly int First, Second;
                                    static C() { C.First = 1; Second = C.First; }
                                    /// <summary><see cref="First"/></summary>
                                    int M() => -C.First + Second;
                                }
                                """;
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument("Test.cs", SourceText.From(Source));
        var root = (await document.GetSyntaxRootAsync())!;
        var field = root.DescendantNodes().OfType<FieldDeclarationSyntax>().Single();
        using var container = new ContainerConfiguration().WithPart<Sst1499MutableStaticFieldCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var editor = await DocumentEditor.CreateAsync(document);
        foreach (var variable in field.Declaration.Variables)
        {
            var diagnostic = Diagnostic.Create(MaintainabilityRules.MutableStaticField, variable.Identifier.GetLocation());
            await ((IAsyncBatchableCodeFix)provider).RegisterEditsAsync(editor, diagnostic, CancellationToken.None);
        }

        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString())
            .IsEqualTo((await CSharpSyntaxTree.ParseText(Expected).GetRootAsync()).NormalizeWhitespace().ToFullString());
    }
}
