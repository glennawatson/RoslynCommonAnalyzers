// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests parity rewrites and rejection of stale non-parity comparisons.</summary>
public class Sst2416SignedRemainderTestCodeFixProviderTests
{
    /// <summary>Verifies only public static single-argument methods qualify as parity helpers.</summary>
    /// <param name="member">The helper-shaped member exposed by the dividend type.</param>
    /// <param name="expected">The resulting parity expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public static bool IsOddInteger(Value value) => true;", "Value.IsOddInteger(n)")]
    [Arguments("public bool IsOddInteger(Value value) => true;", "n % 2 != 0")]
    [Arguments("private static bool IsOddInteger(Value value) => true;", "n % 2 != 0")]
    [Arguments("public static bool IsOddInteger(Value value, int other) => true;", "n % 2 != 0")]
    [Arguments("public static bool IsOddInteger => true;", "n % 2 != 0")]
    public async Task HelperContractControlsReplacementAsync(string member, string expected)
    {
        var source = $$"""struct Value { public static int operator %(Value value, int divisor) => 0; {{member}} } class C { bool M(Value n) => n % 2 == 1; }""";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "M").ExpressionBody!.Expression;
        var diagnostic = Diagnostic.Create(CorrectnessRules.SignedRemainderTest, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2416SignedRemainderTestCodeFixProvider>().CreateContainer();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2416SignedRemainderTestCodeFixProvider>(editor, diagnostic);
        var changed = editor.GetChangedRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(static method => method.Identifier.ValueText == "M").ExpressionBody!.Expression;
        await Assert.That(changed.ToString()).IsEqualTo(expected);
    }

    /// <summary>Verifies single and batch fixes agree on applicability and operand order.</summary>
    /// <param name="expression">The reported expression.</param>
    /// <param name="expected">The replacement, or null if no fix applies.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("true", null)]
    [Arguments("n > 1", null)]
    [Arguments("n == 1", null)]
    [Arguments("n % 3 == 1", null)]
    [Arguments("n % 2 == 2", null)]
    [Arguments("n % divisor == 1", null)]
    [Arguments("n % 2 == divisor", null)]
    [Arguments("n % 2L == 1", null)]
    [Arguments("n % 2 == 1L", null)]
    [Arguments("null % 2 == 1", "null % 2 != 0")]
    [Arguments("1 == n % 2", "int.IsOddInteger(n)")]
    [Arguments("1 != n % 2", "int.IsEvenInteger(n)")]
    public async Task ParityShapeControlsEditsAsync(string expression, string? expected)
    {
        var source = $$"""class C { bool M(int n, int divisor) => {{expression}}; }""";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(CorrectnessRules.SignedRemainderTest, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2416SignedRemainderTestCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(expected is null ? 0 : 1);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2416SignedRemainderTestCodeFixProvider>(editor, diagnostic);
        var result = editor.GetChangedRoot().DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        await Assert.That(result.ToString()).IsEqualTo(expected ?? expression);
        if (expected is null)
        {
            return;
        }

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        await Assert.That(changedRoot.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression.ToString()).IsEqualTo(expected);
    }
}
