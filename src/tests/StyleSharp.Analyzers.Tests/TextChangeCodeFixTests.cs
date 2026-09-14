// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the boundary between lightbulb registration and text-change construction.</summary>
public class TextChangeCodeFixTests
{
    /// <summary>The document before the action is applied.</summary>
    private const string OriginalSource = "class C { }";

    /// <summary>The document after the test edit is applied.</summary>
    private const string ChangedSource = "class D { }";

    /// <summary>Registers with a check that reads only the diagnostic.</summary>
    private const int DiagnosticCheckOverload = 0;

    /// <summary>Registers with a check that reads the syntax root.</summary>
    private const int SyntaxCheckOverload = 1;

    /// <summary>Registers with a check that reads the source text.</summary>
    private const int TextCheckOverload = 2;

    /// <summary>Verifies every worded-check overload waits until invocation before deriving the edits.</summary>
    /// <param name="overload">The registration overload: diagnostic check, syntax check, text check, or text and syntax check.</param>
    /// <param name="applicable">Whether the check words a title for the diagnostic.</param>
    /// <param name="producesEdit">Whether the appender still produces an edit.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments(0, true, true)]
    [Arguments(1, true, true)]
    [Arguments(2, true, true)]
    [Arguments(0, false, true)]
    [Arguments(1, false, true)]
    [Arguments(2, false, true)]
    [Arguments(0, true, false)]
    [Arguments(1, true, false)]
    [Arguments(2, true, false)]
    [Arguments(3, true, true)]
    [Arguments(3, false, true)]
    [Arguments(3, true, false)]
    public async Task RegistrationDefersChangesAsync(int overload, bool applicable, bool producesEdit, CancellationToken cancellationToken)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("CodeFixProject", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(OriginalSource));
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var diagnostic = Diagnostic.Create(new("TEST001", "Test", "Test", "Test", DiagnosticSeverity.Warning, true), root.GetLocation());
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), cancellationToken);
        var appends = 0;
        Diagnostic? appendedFor = null;

        void Append(SourceText text, SyntaxNode _, Diagnostic reported, List<TextChange> changes)
        {
            appends++;
            appendedFor = reported;
            if (producesEdit)
            {
                changes.Add(new(new(0, text.Length), ChangedSource));
            }
        }

        await RegisterOverloadAsync(overload, context, applicable, Append);

        await Assert.That(appends).IsEqualTo(0);
        await Assert.That(actions.Count).IsEqualTo(applicable ? 1 : 0);
        if (!applicable)
        {
            return;
        }

        await Assert.That(actions[0].Title).IsEqualTo("Fix");
        await Assert.That(actions[0].EquivalenceKey).IsEqualTo("Key");
        var applied = await ApplyAsync(document, actions[0], cancellationToken);
        await Assert.That(appends).IsEqualTo(1);
        await Assert.That(appendedFor).IsEqualTo(diagnostic);
        await Assert.That(applied).IsEqualTo(producesEdit ? ChangedSource : OriginalSource);
    }

    /// <summary>Verifies applying an appender that adds nothing returns the original document instance.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ApplyWithoutChangesReturnsOriginalDocumentAsync(CancellationToken cancellationToken)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("CodeFixProject", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(OriginalSource));
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        var diagnostic = Diagnostic.Create(new("TEST001", "Test", "Test", "Test", DiagnosticSeverity.Warning, true), root.GetLocation());

        var applied = await TextChangeCodeFix.ApplyAsync(document, diagnostic, static (_, _, _, _) => { }, cancellationToken);

        await Assert.That(applied).IsSameReferenceAs(document);
    }

    /// <summary>Registers the test action through one of the worded-check overloads.</summary>
    /// <param name="overload">The registration overload to call.</param>
    /// <param name="context">The code fix context.</param>
    /// <param name="applicable">Whether the check words a title.</param>
    /// <param name="append">The change appender.</param>
    /// <returns>A task that represents the asynchronous registration.</returns>
    private static Task RegisterOverloadAsync(int overload, in CodeFixContext context, bool applicable, Action<SourceText, SyntaxNode, Diagnostic, List<TextChange>> append) =>
        overload switch
        {
            DiagnosticCheckOverload => TextChangeCodeFix.RegisterAsync(context, _ => applicable ? "Fix" : null, "Key", append),
            SyntaxCheckOverload => TextChangeCodeFix.RegisterAsync(context, (SyntaxNode _, Diagnostic _) => applicable ? "Fix" : null, "Key", append),
            TextCheckOverload => TextChangeCodeFix.RegisterAsync(context, (SourceText _, Diagnostic _) => applicable ? "Fix" : null, "Key", append),
            _ => TextChangeCodeFix.RegisterAsync(context, (_, _, _) => applicable ? "Fix" : null, "Key", append),
        };

    /// <summary>Invokes an action and reads the resulting document without changing the workspace.</summary>
    /// <param name="document">The original document.</param>
    /// <param name="action">The registered action.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The resulting source text.</returns>
    private static async Task<string> ApplyAsync(Document document, CodeAction action, CancellationToken cancellationToken)
    {
        var operations = await action.GetOperationsAsync(cancellationToken);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var text = await changed.GetTextAsync(cancellationToken);
        return text.ToString();
    }
}
