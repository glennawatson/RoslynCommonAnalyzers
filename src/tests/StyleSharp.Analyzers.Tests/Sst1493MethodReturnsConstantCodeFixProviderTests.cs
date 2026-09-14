// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests constant-method conversion across documents and stale diagnostics.</summary>
public class Sst1493MethodReturnsConstantCodeFixProviderTests
{
    /// <summary>The document containing a single-method test scenario.</summary>
    private const string TestFileName = "Test.cs";

    /// <summary>Checks a method without modifiers retains its trivia when converted to a property.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnmodifiedMethodPreservesDocumentationAsync()
    {
        const string Source = "class C\n{\n    /// <summary>The limit.</summary>\n    int Limit() => 5;\n}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument(TestFileName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var changed = await Sst1493MethodReturnsConstantCodeFixProvider.ConvertAsync(document, method, CancellationToken.None);
        var changedRoot = (await changed.GetDocument(document.Id)!.GetSyntaxRootAsync())!;
        var property = changedRoot.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();
        await Assert.That(property.GetLeadingTrivia().ToFullString()).IsEqualTo(method.GetLeadingTrivia().ToFullString());
        await Assert.That(property.ExpressionBody!.Expression.ToString()).IsEqualTo("5");
    }

    /// <summary>Checks all references in another document, including conditional access, become property reads.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CrossDocumentCallsAndConditionalAccessBecomePropertyReadsAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform);
        var declaration = project.AddDocument("Declaration.cs", "public class C { public int Limit() => 5; }");
        var caller = declaration.Project.AddDocument("Caller.cs", "class D { int? M(C c) => c?.Limit(); int N(C c) => c.Limit() + c.Limit(); }");
        var document = caller.Project.GetDocument(declaration.Id)!;
        var root = (await document.GetSyntaxRootAsync())!;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var changed = await Sst1493MethodReturnsConstantCodeFixProvider.ConvertAsync(document, method, CancellationToken.None);
        var callerRoot = (await changed.GetDocument(caller.Id)!.GetSyntaxRootAsync())!;
        await Assert.That(callerRoot.ToFullString()).IsEqualTo("class D { int? M(C c) => c?.Limit; int N(C c) => c.Limit + c.Limit; }");
        var declarationRoot = (await changed.GetDocument(document.Id)!.GetSyntaxRootAsync())!;
        await Assert.That(declarationRoot.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single().Identifier.ValueText).IsEqualTo("Limit");
    }

    /// <summary>Checks inapplicable method bodies and references leave both registration and conversion unchanged.</summary>
    /// <param name="member">The method and any conflicting members or references.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int Limit() { return 1; return 2; }")]
    [Arguments("abstract int Limit();")]
    [Arguments("int Limit() => 1; string Name => nameof(Limit);")]
    [Arguments("int Limit() => 1; int Limit(int n) => n;")]
    [Arguments("int Limit() => 1; System.Func<int> Getter => Limit;")]
    [Arguments("int Limit() => 1; void Use() { Limit(); }")]
    public async Task UnrewritableMethodAndStaleLocationOfferNoActionAsync(string member)
    {
        using var workspace = new AdhocWorkspace();
        var source = $"abstract class C {{ {member} }}";
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument(TestFileName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.MethodReturnsConstant, method.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1493MethodReturnsConstantCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var changed = await Sst1493MethodReturnsConstantCodeFixProvider.ConvertAsync(document, method, CancellationToken.None);
        await Assert.That(changed).IsSameReferenceAs(document.Project.Solution);
        var stale = Diagnostic.Create(MaintainabilityRules.MethodReturnsConstant, root.GetLocation());
        await provider.RegisterCodeFixesAsync(new(document, stale, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Checks an invalid enumeration does not create a bound reference that prevents conversion.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnboundEnumerationStillAllowsConversionAsync()
    {
        const string Source = "class C { public int GetEnumerator() => 1; void Use() { foreach (var item in this) { } } }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument(TestFileName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var changed = await Sst1493MethodReturnsConstantCodeFixProvider.ConvertAsync(document, method, CancellationToken.None);
        var changedRoot = (await changed.GetDocument(document.Id)!.GetSyntaxRootAsync())!;
        await Assert.That(changedRoot.DescendantNodes().OfType<PropertyDeclarationSyntax>().Single().Identifier.ValueText).IsEqualTo("GetEnumerator");
        await Assert.That(changedRoot.DescendantNodes().OfType<ForEachStatementSyntax>().Single().ToString()).IsEqualTo("foreach (var item in this) { }");
    }
}
