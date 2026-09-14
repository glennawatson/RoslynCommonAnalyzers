// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

using RoslynCommon.Analyzers.Tests;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests CompositeFormat hoisting at different member sites and stale diagnostic rejection.</summary>
public class UseCompositeFormatCodeFixProviderTests
{
    /// <summary>The cached framework references from before CompositeFormat was introduced.</summary>
    private static readonly Task<ImmutableArray<MetadataReference>> LegacyReferences = AnalyzerFrameworks.NetStandard20.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

    /// <summary>Checks hoisted fields precede their use and retain constant values and collision-free names.</summary>
    /// <param name="members">The members containing a format call.</param>
    /// <param name="fieldName">The expected hoisted field name.</param>
    /// <param name="formatSource">The expression retained in the parsed format initializer.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("public C() { _ = string.Format(\"{0}\", 1); }", "CFormat", "\"{0}\"")]
    [Arguments("public string Value => string.Format(\"{0}\", 1);", "ValueFormat", "\"{0}\"")]
    [Arguments("static string Value = string.Format(\"{0}\", 1);", "Format", "\"{0}\"")]
    [Arguments("string M() { const string format = \"{0}\"; return string.Format(format, 1); }", "MFormat", "\"{0}\"")]
    [Arguments("event System.Action MFormat { add {} remove {} } int MFormat2 => 1; string M() => string.Format(\"{0}\", 1);", "MFormat3", "\"{0}\"")]
    [Arguments("const string Pattern = \"{0}\"; string M() => string.Format(Pattern, 1);", "MFormat", "Pattern")]
    public async Task HoistRespectsMemberScopeAndNamesAsync(string members, string fieldName, string formatSource)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("CompositeFormat", LanguageNames.CSharp).AddMetadataReferences(RuntimeMetadataReferences.Platform);
        project = project.WithCompilationOptions(project.CompilationOptions!.WithOutputKind(OutputKind.DynamicallyLinkedLibrary));
        var document = project.AddDocument("Test.cs", $"namespace N\n{{\n    class C\n    {{\n        {members}\n    }}\n}}");
        var root = (await document.GetSyntaxRootAsync())!;
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var diagnostic = CreateDiagnostic(root, invocation.Span);
        using var container = new ContainerConfiguration().WithPart<Psh1223UseCompositeFormatCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var owner = changedRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        var field = (FieldDeclarationSyntax)owner.Members[0];
        await Assert.That(field.Declaration.Variables[0].Identifier.ValueText).IsEqualTo(fieldName);
        await Assert.That(field.Declaration.Type.ToString()).IsEqualTo("global::System.Text.CompositeFormat");
        await Assert.That(field.Declaration.Variables[0].Initializer!.Value.ToString()).IsEqualTo($"global::System.Text.CompositeFormat.Parse({formatSource})");
        await Assert.That(changedRoot.ToFullString()).Contains($"string.Format(global::System.Globalization.CultureInfo.CurrentCulture, {fieldName}, 1)");
        var compilation = (await changed.Project.GetCompilationAsync())!;
        await Assert.That(compilation.GetDiagnostics().Where(static item => item.Severity == DiagnosticSeverity.Error).ToArray()).IsEmpty();
    }

    /// <summary>Checks a diagnostic that outlives the format shape or hoist site offers no action.</summary>
    /// <param name="source">The current source.</param>
    /// <param name="target">The diagnostic text.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("class C { int M() => 1; }", "1")]
    [Arguments("class C { string M() => string.Concat(\"x\", 1); }", "string.Concat")]
    [Arguments("interface C { string M() => string.Format(\"{0}\", 1); }", "string.Format")]
    [Arguments("string.Format(\"{0}\", 1);", "string.Format")]
    [Arguments("class C { string M(string format) => string.Format(format, 1); }", "string.Format")]
    [Arguments("class C {\n#region Format\nstring M() => string.Format(\"{0}\", 1);\n#endregion\n}", "string.Format")]
    [Arguments("using System.Globalization; class C { string M() { int CultureInfo = 1; return string.Format(\"{0}\", 1); } }", "string.Format")]
    [Arguments("using System.Globalization; using Other; namespace Other { class CultureInfo {} } class C { string M() => string.Format(\"{0}\", 1); }", "string.Format")]
    public async Task StaleOrUnsafeHoistIsRejectedAsync(string source, string target)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("CompositeFormat", LanguageNames.CSharp).AddMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = CreateDiagnostic(root, new(source.IndexOf(target, StringComparison.Ordinal), target.Length));
        using var container = new ContainerConfiguration().WithPart<Psh1223UseCompositeFormatCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(0);
    }

    /// <summary>Checks applying a stale diagnostic on an older framework leaves its source unchanged.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task ApplicationRechecksFrameworkOverloadAvailabilityAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("LegacyFormat", LanguageNames.CSharp).AddMetadataReferences(await LegacyReferences);
        var document = project.AddDocument("Legacy.cs", "class C { string M() => string.Format(\"{0}\", 1); }");
        var root = (await document.GetSyntaxRootAsync())!;
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var diagnostic = CreateDiagnostic(root, invocation.Span);
        using var container = new ContainerConfiguration().WithPart<Psh1223UseCompositeFormatCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetSyntaxRootAsync())!.ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Creates a diagnostic over the supplied source span.</summary>
    /// <param name="root">The source root.</param>
    /// <param name="span">The diagnostic span.</param>
    /// <returns>The diagnostic.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Diagnostic CreateDiagnostic(SyntaxNode root, TextSpan span) =>
        Diagnostic.Create(new("PSH1223", "Test", "Test", "Test", DiagnosticSeverity.Info, true), Location.Create(root.SyntaxTree, span));
}
