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
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests value code fixes when diagnostic spans and properties no longer describe a valid edit.</summary>
public class ModernSyntaxValueCodeFixProviderTests
{
    /// <summary>The source prefix shared by statement-level code-fix tests.</summary>
    private const string SourcePrefix = "class C { void M() { ";

    /// <summary>The source suffix shared by statement-level code-fix tests.</summary>
    private const string SourceSuffix = " } }";

    /// <summary>The diagnostic for expression statements whose values are ignored.</summary>
    private const string IgnoredValueDiagnosticId = "SST2221";

    /// <summary>Verifies a standalone assignment cannot remove the syntax root from a document.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemovingStandaloneAssignmentPreservesDocumentAsync()
    {
        using var workspace = new AdhocWorkspace();
        var (document, _) = CreateDocument(workspace, "SST2222", "[|value = 1|];", null, null);
        var statement = SyntaxFactory.ParseStatement("value = 1;");
        var diagnostic = Diagnostic.Create(new("SST2222", "Test", "Test", "Tests", DiagnosticSeverity.Warning, true), statement.GetLocation());
        await Assert.That(ModernSyntaxValueCodeFixProvider.Apply(document, statement, diagnostic)).IsSameReferenceAs(document);
    }

    /// <summary>Verifies Fix All coalesces duplicate edits and preserves statements with no discardable value.</summary>
    /// <param name="fixable">Whether the selected statement produces a value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task FixAllHandlesDuplicateAndStaleIgnoredValuesAsync(bool fixable)
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<ModernSyntaxValueCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var body = fixable ? "[|new object()|];" : "[|M()|];";
        var (document, diagnostic) = CreateDocument(workspace, IgnoredValueDiagnosticId, body, null, null);
        var context = new FixAllContext(
            document,
            provider,
            FixAllScope.Document,
            IgnoredValueDiagnosticId,
            provider.FixableDiagnosticIds,
            new SelectedDiagnostics([diagnostic, diagnostic]),
            CancellationToken.None);
        var action = (await provider.GetFixAllProvider()!.GetFixAsync(context))!;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().SingleOrDefault()?.ChangedSolution.GetDocument(document.Id) ?? document;
        var expected = (await CSharpSyntaxTree.ParseText(
            $"{SourcePrefix}{(fixable ? "_ = new object();" : "M();")}{SourceSuffix}").GetRootAsync()).NormalizeWhitespace().ToFullString();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies a mismatched syntax shape does not register, apply, or batch an edit.</summary>
    /// <param name="id">The stale diagnostic ID.</param>
    /// <param name="body">The method body with its diagnostic span selected.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("SST9999", "[|M()|];")]
    [Arguments("SST2220", "[|M()|];")]
    [Arguments("SST2220", "_ = $\"{[|1|]}\";")]
    [Arguments("SST2220", "_ = $\"{[|1.ToString(\"\")|]}\";")]
    [Arguments("SST2220", "string format = \"N\"; _ = $\"{[|1.ToString(format)|]}\";")]
    [Arguments("SST2220", "_ = $\"{[|1.ToString(\"N\", null)|]}\";")]
    [Arguments("SST2221", "[|int value = 0;|]")]
    [Arguments("SST2221", "[|M()|];")]
    [Arguments("SST2221", "[|Missing()|];")]
    [Arguments("SST2221", "[|_ = 1|];")]
    [Arguments("SST2222", "int value = 0; [|value++|];")]
    [Arguments("SST2222", "[|int first = 1, second = 2;|]")]
    [Arguments("SST2222", "[|int value;|]")]
    [Arguments("SST2222", "[|M()|];")]
    [Arguments("SST2223", "[|if (true) { M(); M(); }|]")]
    [Arguments("SST2223", "_ = [|1 + 2|];")]
    [Arguments("SST2223", "_ = [|(string)null ?? \"value\"|];")]
    [Arguments("SST2224", "_ = [|new { }|];")]
    [Arguments("SST2224", "_ = [|new { 1 + 2 }|];")]
    [Arguments("SST2224", "[|M()|];")]
    [Arguments("SST2225", "[|M()|];")]
    [Arguments("SST2226", "[|M()|];")]
    [Arguments("SST2227", "[|M()|];")]
    [Arguments("SST2231", "_ = [|1 is int value|];")]
    [Arguments("SST2231", "_ = [|new object() is not string|];")]
    [Arguments("SST2231", "_ = [|1 + 2|];")]
    [Arguments("SST2232", "[|int value;|]")]
    [Arguments("SST2232", "[|M()|];")]
    [Arguments("SST2232", "_ = [|nameof(C)|];")]
    [Arguments("SST2232", "_ = [|nameof(System.Collections.Generic.List<>)|];")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MismatchedSyntaxHasNoFixAsync(string id, string body) => VerifyNoFixAsync(id, body);

    /// <summary>Verifies unsupported delegate types and mismatched lambda arities have no local-function fix.</summary>
    /// <param name="body">The delegate declaration with its diagnostic span selected.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("[|M()|];")]
    [Arguments("[|System.Func<int> a = () => 1, b = () => 2;|]")]
    [Arguments("[|System.Func<int> a;|]")]
    [Arguments("[|System.Action a = () => { };|]")]
    [Arguments("[|System.Predicate<int> a = value => true;|]")]
    [Arguments("[|System.Func<int> a = value => value;|]")]
    [Arguments("[|System.Func<int, int> a = () => 1;|]")]
    [Arguments("[|System.Func<int> a = delegate { return 1; };|]")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnsupportedDelegateHasNoFixAsync(string body) => VerifyNoFixAsync("SST2228", body);

    /// <summary>Verifies missing or empty type metadata does not create invalid casts.</summary>
    /// <param name="id">The cast diagnostic.</param>
    /// <param name="body">The selected cast or loop.</param>
    /// <param name="key">The diagnostic property key.</param>
    /// <param name="value">The diagnostic property value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("SST2225", "[|foreach (string item in new object[0]) { }|]", null, null)]
    [Arguments("SST2225", "[|foreach (string item in new object[0]) { }|]", "ElementType", " ")]
    [Arguments("SST2226", "_ = [|(long)1|];", null, null)]
    [Arguments("SST2226", "_ = [|(long)1|];", "Type", " ")]
    [Arguments("SST2227", "[|if (true) M();|]", null, null)]
    [Arguments("SST2227", "[|if (true) M();|]", "FoldKind", "Assignment")]
    [Arguments("SST2227", "while (true) [|if (true) M();|]", "FoldKind", "Assignment")]
    [Arguments("SST2227", "M(); [|if (true) M();|]", "FoldKind", "Throw")]
    [Arguments("SST2227", "M(); [|if (true) throw new System.Exception();|]", "FoldKind", "unknown")]
    [Arguments("SST2227", "M(); [|if (true) throw new System.Exception();|]", "FoldKind", "Throw")]
    [Arguments("SST2227", "int a, b; [|if (true) throw new System.Exception();|]", "FoldKind", "Throw")]
    [Arguments("SST2227", "int a; [|if (true) throw new System.Exception();|]", "FoldKind", "Throw")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task IncompletePropertiesHaveNoFixAsync(string id, string body, string? key, string? value) => VerifyNoFixAsync(id, body, key, value);

    /// <summary>Verifies alternate value syntax produces the expected individual and batch edit.</summary>
    /// <param name="id">The diagnostic selecting the rewrite.</param>
    /// <param name="body">The method body with the diagnostic span selected.</param>
    /// <param name="expectedBody">The expected rewritten body.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("SST2223", "string value = null; _ = [|value ?? (value = \"a\")|];", "string value = null; _ = value ??= \"a\";")]
    [Arguments("SST2228", "[|System.Action<int> action = value => { };|]", "void action(int value) { }")]
    [Arguments("SST2228", "[|System.Func<int, int, int> add = (a, b) => a + b;|]", "int add(int a, int b) => a + b;")]
    [Arguments("SST2228", "[|global::Func<int, int> action = value => value;|]", "int action(int value) => value;")]
    [Arguments("SST2231", "_ = [|new object() is not object { }|];", "_ = new object() is null;")]
    [Arguments("SST2231", "_ = [|new object() is not object|];", "_ = new object() is null;")]
    [Arguments("SST2231", "_ = [|new object() is object|];", "_ = new object() is not null;")]
    [Arguments("SST2222", "int value = 1; return ([|value++|]);", "int value = 1; return (value);")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task AlternateValueSyntaxIsRewrittenAsync(string id, string body, string expectedBody) => VerifyEditAsync(id, body, expectedBody);

    /// <summary>Verifies the analyzer's negated object pattern diagnostic has a null-pattern fix.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NegatedObjectPatternIsReportedAndFixedAsync() =>
        CSharpCodeFixVerifier<ModernSyntaxValueAnalyzer, ModernSyntaxValueCodeFixProvider>.VerifyCodeFixAsync(
            "class C { bool M(object value) => value is not {|SST2231:object|}; }",
            "class C { bool M(object value) => value is null; }");

    /// <summary>Verifies null fallback folds work for assignments after intervening statements.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NullFallbackFoldsIntoPreviousAssignmentAsync() =>
        VerifyEditAsync(
            "SST2227",
            "string value; M(); value = null; [|if (value == null) { value = \"fallback\"; }|]",
            "string value; M(); value = null ?? \"fallback\";",
            "FoldKind",
            ModernSyntaxValueAnalyzer.AssignmentFold);

    /// <summary>Checks that a stale diagnostic cannot register or apply an edit.</summary>
    /// <param name="id">The diagnostic ID.</param>
    /// <param name="body">The selected method body.</param>
    /// <param name="key">An optional property key.</param>
    /// <param name="value">The optional property value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    private static async Task VerifyNoFixAsync(string id, string body, string? key = null, string? value = null)
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<ModernSyntaxValueCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var (document, diagnostic) = CreateDocument(workspace, id, body, key, value);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = await document.GetSemanticModelAsync();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(ModernSyntaxValueCodeFixProvider.Apply(document, root, model, diagnostic)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        ModernSyntaxValueCodeFixProvider.RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
        _ = ModernSyntaxValueCodeFixProvider.TryGetBatchEditSpan(root, diagnostic, out _);
    }

    /// <summary>Checks the resulting syntax for individual and batch rewrites.</summary>
    /// <param name="id">The diagnostic ID.</param>
    /// <param name="body">The selected method body.</param>
    /// <param name="expectedBody">The rewritten body.</param>
    /// <param name="key">An optional property key.</param>
    /// <param name="value">The optional property value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    private static async Task VerifyEditAsync(string id, string body, string expectedBody, string? key = null, string? value = null)
    {
        using var workspace = new AdhocWorkspace();
        using var container = new ContainerConfiguration().WithPart<ModernSyntaxValueCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var (document, diagnostic) = CreateDocument(workspace, id, body, key, value);
        var root = (await document.GetSyntaxRootAsync())!;
        var model = await document.GetSemanticModelAsync();
        var changed = ModernSyntaxValueCodeFixProvider.Apply(document, root, model, diagnostic);
        var expected = (await CSharpSyntaxTree.ParseText($"{SourcePrefix}{expectedBody}{SourceSuffix}").GetRootAsync()).NormalizeWhitespace().ToFullString();
        await Assert.That((await changed.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expected);
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var applied = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await applied.GetSyntaxRootAsync())!.NormalizeWhitespace().ToFullString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        ModernSyntaxValueCodeFixProvider.RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().NormalizeWhitespace().ToFullString()).IsEqualTo(expected);
        await Assert.That(ModernSyntaxValueCodeFixProvider.TryGetBatchEditSpan(root, diagnostic, out _)).IsTrue();
    }

    /// <summary>Creates a document and diagnostic from a selected statement body.</summary>
    /// <param name="workspace">The owning workspace.</param>
    /// <param name="id">The diagnostic ID.</param>
    /// <param name="body">The marked method body.</param>
    /// <param name="key">An optional property key.</param>
    /// <param name="value">The optional property value.</param>
    /// <returns>The document and its selected diagnostic.</returns>
    private static (Document Document, Diagnostic Diagnostic) CreateDocument(AdhocWorkspace workspace, string id, string body, string? key, string? value)
    {
        const string OpenMarker = "[|";
        const string CloseMarker = "|]";
        var start = body.IndexOf(OpenMarker, StringComparison.Ordinal);
        var end = body.IndexOf(CloseMarker, StringComparison.Ordinal);
        var unmarked = body.Remove(end, CloseMarker.Length).Remove(start, OpenMarker.Length);
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var document = project.AddDocument("Test.cs", SourceText.From($"{SourcePrefix}{unmarked}{SourceSuffix}"));
        var span = new TextSpan(SourcePrefix.Length + start, end - start - OpenMarker.Length);
        var descriptor = new DiagnosticDescriptor(id, id, id, "Tests", DiagnosticSeverity.Warning, isEnabledByDefault: true);
        var properties = key is null ? ImmutableDictionary<string, string?>.Empty : ImmutableDictionary<string, string?>.Empty.Add(key, value);
        return (document, Diagnostic.Create(descriptor, Location.Create("Test.cs", span, default), properties));
    }

    /// <summary>Supplies a fixed document diagnostic set to a Fix All operation.</summary>
    /// <param name="diagnostics">The selected diagnostics, including any duplicates.</param>
    private sealed class SelectedDiagnostics(ImmutableArray<Diagnostic> diagnostics) : FixAllContext.DiagnosticProvider
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult(Enumerable.Empty<Diagnostic>());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
