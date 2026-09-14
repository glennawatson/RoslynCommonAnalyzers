// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests flags operand removal and stale diagnostic rejection.</summary>
public class Sst2495RedundantFlagsOperandCodeFixProviderTests
{
    /// <summary>Checks both surviving operands, parenthesized operands, and unrelated syntax.</summary>
    /// <param name="expression">The expression containing the diagnosed flag.</param>
    /// <param name="expected">The replacement expression, or null when the fix is unavailable.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("F.A | F.Both", "F.Both")]
    [Arguments("F.Both | F.A", "F.Both")]
    [Arguments("((F.A)) | F.Both", "F.Both")]
    [Arguments("F.Both | ((F.A))", "F.Both")]
    [Arguments("F.A & F.Both", null)]
    [Arguments("F.A", null)]
    public async Task OperandShapeControlsRegistrationAndBatchEditAsync(string expression, string? expected)
    {
        var source = $"enum F {{ A = 1, Both = 3 }} class C {{ F M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var operand = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Single(static access => access.Name.Identifier.ValueText == "A");
        var diagnostic = Diagnostic.Create(CorrectnessRules.RedundantFlagsOperand, operand.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2495RedundantFlagsOperandCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(expected is null ? 0 : 1);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        var expectedSource = $"enum F {{ A = 1, Both = 3 }} class C {{ F M() => {expected ?? expression}; }}";
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expectedSource);
        if (actions.Count == 0)
        {
            return;
        }

        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expectedSource);
    }
}
