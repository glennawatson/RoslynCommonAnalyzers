// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests stale language-style diagnostics against registration, application, and batch editing.</summary>
public class LanguageStyleCodeFixProviderTests
{
    /// <summary>Checks malformed or changed syntax produces neither an action nor an edit.</summary>
    /// <param name="id">The stale diagnostic identifier.</param>
    /// <param name="body">The method body.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <returns>The assertion task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("SST1193", "switch (flag) { case true: var x = new C(); x.P = 1; break; }", "new C()")]
    [Arguments("SST1193", "var x = new C();", "new C()")]
    [Arguments("SST1193", "var x = new C(); Log();", "new C()")]
    [Arguments("SST1193", "var x = new C(); x.P += 1;", "new C()")]
    [Arguments("SST1193", "var x = new C(); x = y;", "new C()")]
    [Arguments("SST1193", "var x = new C(); this.P = y;", "new C()")]
    [Arguments("SST1193", "var x = new C(); y.P = 1;", "new C()")]
    [Arguments("SST1193", "C x = new C(), y = new C();", "new C()")]
    [Arguments("SST1193", "return new C();", "new C()")]
    [Arguments("SST1193", "return 1;", "1")]
    [Arguments("SST1194", "var x = new C();", "new C()")]
    [Arguments("SST1194", "var x = new C(); x = y;", "new C()")]
    [Arguments("SST1194", "var x = new C(); Add(1);", "new C()")]
    [Arguments("SST1194", "var x = new C(); this.Add(1);", "new C()")]
    [Arguments("SST1194", "var x = new C(); x.Remove(1);", "new C()")]
    [Arguments("SST1194", "var x = new C(); x.Add();", "new C()")]
    [Arguments("SST1194", "var x = new C(); y.Add(1);", "new C()")]
    [Arguments("SST1194", "return 1;", "1")]
    public Task StaleInitializerDiagnosticDoesNotRegisterOrEditAsync(string id, string body, string target) => VerifyStaleDiagnosticAsync(id, body, target);

    /// <summary>Checks conditional and name fixes reject diagnostics whose syntax no longer matches.</summary>
    /// <param name="id">The stale diagnostic identifier.</param>
    /// <param name="body">The method body.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <returns>The assertion task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("SST1195", "return flag ? x : y;", "flag ? x : y")]
    [Arguments("SST1195", "return x == y ? x : y;", "x == y ? x : y")]
    [Arguments("SST1195", "return null == null ? x : y;", "null == null ? x : y")]
    [Arguments("SST1195", "return x > null ? y : x;", "x > null ? y : x")]
    [Arguments("SST1195", "return x == null ? y : z;", "x == null ? y : z")]
    [Arguments("SST1195", "return x == null ? y : longer;", "x == null ? y : longer")]
    [Arguments("SST1195", "return x;", "x")]
    [Arguments("SST1196", "return x;", "x")]
    [Arguments("SST1196", "return flag ? x : y;", "flag ? x : y")]
    [Arguments("SST1196", "return x == null ? y : x.P;", "x == null ? y : x.P")]
    [Arguments("SST1196", "return x == null ? null : x;", "x == null ? null : x")]
    [Arguments("SST1196", "return x == null ? null : y.P;", "x == null ? null : y.P")]
    [Arguments("SST1197", "return 1;", "return")]
    [Arguments("SST1197", "while (flag) if (flag) return 1;", "if")]
    [Arguments("SST1197", "if (flag) Log(); return 2;", "if")]
    [Arguments("SST1197", "if (flag) {} return 2;", "if")]
    [Arguments("SST1197", "if (flag) { Log(); } return 2;", "if")]
    [Arguments("SST1197", "if (flag) return; return 2;", "if")]
    [Arguments("SST1197", "if (flag) return 1;", "if")]
    [Arguments("SST1197", "if (flag) return 1; Log();", "if")]
    [Arguments("SST1197", "if (flag) return 1; return;", "if")]
    [Arguments("SST1197", "if (a ? b : c) return 1; return 2;", "if")]
    [Arguments("SST1197", "if (flag) return a ? b : c; return 2;", "if")]
    [Arguments("SST1197", "if (flag) return F(a ? b : c); return 2;", "if")]
    [Arguments("SST1197", "if (flag) return 1; return a ? b : c;", "if")]
    [Arguments("SST1197", "if (flag) return 1;\n#if FEATURE\nreturn 3;\n#endif\nreturn 2;", "if")]
    [Arguments("SST1198", "return 1;", "return")]
    [Arguments("SST1198", "if (flag) x = 1;", "if")]
    [Arguments("SST1198", "if (flag) Log(); else x = 2;", "if")]
    [Arguments("SST1198", "if (flag) x = 1; else Log();", "if")]
    [Arguments("SST1198", "if (flag) {} else x = 2;", "if")]
    [Arguments("SST1198", "if (flag) { return; } else x = 2;", "if")]
    [Arguments("SST1198", "if (flag) x += 1; else x = 2;", "if")]
    [Arguments("SST1199", "return typeof(C).FullName;", "typeof(C).FullName")]
    [Arguments("SST1199", "return x.Name;", "x.Name")]
    [Arguments("SST1199", "return x;", "x")]
    [Arguments("SST0000", "return x;", "x")]
    public Task StaleDiagnosticDoesNotRegisterOrEditAsync(string id, string body, string target) => StaleInitializerDiagnosticDoesNotRegisterOrEditAsync(id, body, target);

    /// <summary>Checks diagnostic dispatch rejects identifiers that resemble supported rules.</summary>
    /// <param name="id">The unsupported diagnostic identifier.</param>
    /// <returns>The assertion task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("SST1190")]
    [Arguments("SST1192")]
    [Arguments("SST119A")]
    [Arguments("SST1200")]
    [Arguments("UNKNOWN")]
    [Arguments("OTHER")]
    [Arguments("ALT1193")]
    [Arguments("ALT1194")]
    [Arguments("ALT1195")]
    [Arguments("ALT1196")]
    [Arguments("ALT1197")]
    [Arguments("ALT1198")]
    [Arguments("ALT1199")]
    public Task UnsupportedDiagnosticIdentifiersAreIgnoredAsync(string id) => VerifyStaleDiagnosticAsync(id, "return x;", "x");

    /// <summary>Checks reversed null comparisons and direct embedded statements produce the expected syntax.</summary>
    /// <param name="id">The diagnostic identifier.</param>
    /// <param name="body">The original method body.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <param name="expected">The rewritten method body.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("SST1195", "return null != x ? x : y;", "null != x ? x : y", "return x ?? y;")]
    [Arguments("SST1196", "return null != x ? x.P : null;", "null != x ? x.P : null", "return x?.P;")]
    [Arguments("SST1197", "Log(); if (flag) return 1; return 2;", "if", "Log(); return flag ? 1 : 2;")]
    [Arguments("SST1198", "if (flag) x = 1; else x = 2;", "if", "x = flag ? 1 : 2;")]
    public async Task ApplyBuildsEquivalentReplacementSyntaxAsync(string id, string body, string target, string expected)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("LanguageStyle", LanguageNames.CSharp).AddDocument("Test.cs", $"class C {{ object M() {{ {body} }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var changed = LanguageStyleCodeFixProvider.Apply(document, root, CreateDiagnostic(root, id, target));
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        await Assert.That(SyntaxFactory.AreEquivalent(changedRoot, SyntaxFactory.ParseCompilationUnit($"class C {{ object M() {{ {expected} }} }}"))).IsTrue();
    }

    /// <summary>Verifies initializer fixes preserve intervening directives by refusing to move statements across them.</summary>
    /// <param name="id">The initializer diagnostic.</param>
    /// <param name="following">The absorbed statement.</param>
    /// <param name="gap">The directive trivia between the statements.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("SST1193", "x.P = 1;", "#if FEATURE\nLog();\n#endif")]
    [Arguments("SST1194", "x.Add(1);", "#if FEATURE\nLog();\n#endif")]
    [Arguments("SST1193", "x.P = 1;", "#region Keep\n#endregion")]
    [Arguments("SST1194", "x.Add(1);", "#region Keep\n#endregion")]
    [Arguments("SST1193", "x.P = 1;", "#if true")]
    [Arguments("SST1194", "x.Add(1);", "#if true")]
    [Arguments("SST1193", "x.P =\n#if true\n1\n#else\n2\n#endif\n;", "")]
    [Arguments("SST1194", "x.Add(\n#if true\n1\n#else\n2\n#endif\n);", "")]
    public async Task InitializerFixPreservesInterveningDirectivesAsync(string id, string following, string gap)
    {
        using var workspace = new AdhocWorkspace();
        var source = $"class C {{ object M() {{ var x = new C();\n{gap}\n{following} }} }}";
        var document = workspace.AddProject("InitializerDirectives", LanguageNames.CSharp).AddDocument("Directive.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = CreateDiagnostic(root, id, "new C()");
        using var container = new ContainerConfiguration().WithPart<LanguageStyleCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var changed = LanguageStyleCodeFixProvider.Apply(document, root, diagnostic);
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        await Assert.That(changedRoot.ToFullString()).IsEqualTo(source);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies directives surrounding both statements are retained when the initializer fix is safe.</summary>
    /// <param name="id">The initializer diagnostic.</param>
    /// <param name="following">The statement to absorb.</param>
    /// <param name="initializer">The expected initializer contents.</param>
    /// <param name="comment">The exterior comment to retain.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("SST1193", "x.P = 1;", "P=1", "")]
    [Arguments("SST1194", "x.Add(1);", "1", "")]
    [Arguments("SST1193", "x.P = 1;", "P=1", " /* keep */")]
    [Arguments("SST1194", "x.Add(1);", "1", " // keep")]
    public async Task InitializerFixRetainsSurroundingDirectivesAsync(string id, string following, string initializer, string comment)
    {
        using var workspace = new AdhocWorkspace();
        var source = $"#region Keep\nclass C {{ object M() {{\n#if true\nvar x = new C(); {following}{comment}\n#endif\nreturn x; }} }}\n#endregion\n";
        var document = workspace.AddProject("SurroundingDirectives", LanguageNames.CSharp).AddDocument("Directive.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = CreateDiagnostic(root, id, "new C()");
        var expected = $"#region Keep\nclass C {{ object M() {{\n#if true\nvar x = new C(){{{initializer}}}; {comment}\n#endif\nreturn x; }} }}\n#endregion\n";
        var changed = LanguageStyleCodeFixProvider.Apply(document, root, diagnostic);
        await Assert.That((await changed.GetSyntaxRootAsync())!.ToFullString()).IsEqualTo(expected);
        using var container = new ContainerConfiguration().WithPart<LanguageStyleCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Builds a diagnostic at exact source text without running the analyzer.</summary>
    /// <param name="root">The source root.</param>
    /// <param name="id">The diagnostic identifier.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <returns>The synthetic stale diagnostic.</returns>
    internal static Diagnostic CreateDiagnostic(SyntaxNode root, string id, string target)
    {
        var descriptor = new DiagnosticDescriptor(id, "Test", "Test", "Test", DiagnosticSeverity.Info, true);
        var span = new TextSpan(root.ToFullString().IndexOf(target, StringComparison.Ordinal), target.Length);
        return Diagnostic.Create(descriptor, Location.Create(root.SyntaxTree, span));
    }

    /// <summary>Verifies all code-fix entry points reject a stale diagnostic.</summary>
    /// <param name="id">The diagnostic identifier.</param>
    /// <param name="body">The method body.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <returns>The assertion task.</returns>
    private static async Task VerifyStaleDiagnosticAsync(string id, string body, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("StaleLanguageStyle", LanguageNames.CSharp).AddDocument("Test.cs", $"class C {{ object M() {{ {body} }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = CreateDiagnostic(root, id, target);
        using var container = new ContainerConfiguration().WithPart<LanguageStyleCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(0);
        await Assert.That(LanguageStyleCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }
}
