// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using RoslynCommon.Analyzers.CodeFixes;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the target code fix registrations that decide per diagnostic and build the edit when the action runs.</summary>
public class TargetCodeFixTests
{
    /// <summary>The source with two reportable statements.</summary>
    private const string Source = "class C { void M() { A(); B(); } }";

    /// <summary>The source after the first statement's call is renamed.</summary>
    private const string FirstRenamed = "class C { void M() { Z(); B(); } }";

    /// <summary>The source after the second statement's call is renamed.</summary>
    private const string SecondRenamed = "class C { void M() { A(); Z(); } }";

    /// <summary>The first statement's call expression.</summary>
    private const string FirstCall = "A()";

    /// <summary>The second statement's call expression.</summary>
    private const string SecondCall = "B()";

    /// <summary>The diagnostic property naming the call a diagnostic reports.</summary>
    private const string CallProperty = "Call";

    /// <summary>The number of statements the fixture reports.</summary>
    private const int ReportedStatementCount = 2;

    /// <summary>The document name used by the workspace fixtures.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>The code action title used by fixed-title registrations.</summary>
    private const string Title = "Rename the call";

    /// <summary>The equivalence key used by fixed-key registrations.</summary>
    private const string Key = "rename";

    /// <summary>The diagnostic reported on the first statement.</summary>
    private static readonly DiagnosticDescriptor Rule = new("TEST001", "First", "First", "Testing", DiagnosticSeverity.Warning, true);

    /// <summary>The diagnostic reported on the second statement.</summary>
    private static readonly DiagnosticDescriptor OtherRule = new("TEST002", "Second", "Second", "Testing", DiagnosticSeverity.Warning, true);

    /// <summary>Verifies a predicate registration offers only diagnostics that still match and re-resolves the diagnostic on apply.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PredicateRegistrationOffersMatchingDiagnosticsAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace);
        var actions = await CollectAsync(document, static context => TargetCodeFix.RegisterAsync(context, Title, Key, IsSecondStatement, RenameReported));

        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].Title).IsEqualTo(Title);
        await Assert.That(actions[0].EquivalenceKey).IsEqualTo(Key);
        await Assert.That(await ApplyAsync(actions[0], document)).IsEqualTo(SecondRenamed);
    }

    /// <summary>Verifies a predicate registration with an asynchronous apply hands the diagnostic to the edit.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PredicateRegistrationAppliesAsynchronouslyAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace);
        var actions = await CollectAsync(document, static context => TargetCodeFix.RegisterAsync(context, Title, Key, IsSecondStatement, RenameReportedAsync));

        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(await ApplyAsync(actions[0], document)).IsEqualTo(SecondRenamed);
    }

    /// <summary>Verifies a diagnostic-titled predicate registration skips untitled and non-matching diagnostics.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticTitledPredicateRegistrationSkipsUntitledDiagnosticsAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace);
        var actions = await CollectAsync(
            document,
            static context => TargetCodeFix.RegisterAsync(context, TitleForOtherRule, static diagnostic => diagnostic.Id, IsSecondStatement, RenameReported),
            extraFirstStatementRule: OtherRule);

        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].Title).IsEqualTo(OtherRule.Title.ToString());
        await Assert.That(actions[0].EquivalenceKey).IsEqualTo(OtherRule.Id);
        await Assert.That(await ApplyAsync(actions[0], document)).IsEqualTo(SecondRenamed);
    }

    /// <summary>Verifies a diagnostic-titled predicate registration with an asynchronous apply hands the root and diagnostic to the edit.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticTitledPredicateRegistrationAppliesAsynchronouslyAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace);
        var actions = await CollectAsync(
            document,
            static context => TargetCodeFix.RegisterAsync(
                context,
                TitleForOtherRule,
                static diagnostic => diagnostic.Id,
                IsSecondStatement,
                static (current, root, diagnostic, _) => Task.FromResult(RenameReported(current, root, diagnostic))),
            extraFirstStatementRule: OtherRule);

        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].EquivalenceKey).IsEqualTo(OtherRule.Id);
        await Assert.That(await ApplyAsync(actions[0], document)).IsEqualTo(SecondRenamed);
    }

    /// <summary>Verifies an unconditional registration offers every diagnostic and hands it to the edit.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnconditionalRegistrationOffersEveryDiagnosticAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace);
        var actions = await CollectAsync(document, static context => TargetCodeFix.RegisterAsync(context, Title, Key, RenameReportedAsync));

        await Assert.That(actions.Count).IsEqualTo(ReportedStatementCount);
        await Assert.That(actions[1].Title).IsEqualTo(Title);
        await Assert.That(await ApplyAsync(actions[0], document)).IsEqualTo(FirstRenamed);
        await Assert.That(await ApplyAsync(actions[1], document)).IsEqualTo(SecondRenamed);
    }

    /// <summary>Verifies an unconditional registration titles each action from its diagnostic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnconditionalRegistrationTitlesFromTheDiagnosticAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace);
        var actions = await CollectAsync(document, static context => TargetCodeFix.RegisterAsync(context, static diagnostic => diagnostic.Id, Key, RenameReportedAsync));

        await Assert.That(actions.Count).IsEqualTo(ReportedStatementCount);
        await Assert.That(actions[0].Title).IsEqualTo(Rule.Id);
        await Assert.That(actions[1].Title).IsEqualTo(OtherRule.Id);
        await Assert.That(actions[1].EquivalenceKey).IsEqualTo(Key);
        await Assert.That(await ApplyAsync(actions[0], document)).IsEqualTo(FirstRenamed);
    }

    /// <summary>Verifies a rewrite registration offers only resolved targets and replaces the target with its rewrite on apply.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewriteRegistrationReplacesTheResolvedTargetAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace);
        var actions = await CollectAsync(document, static context => TargetCodeFix.RegisterAsync(context, Title, Key, ResolveSecondStatement, RenameCall));

        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].Title).IsEqualTo(Title);
        await Assert.That(actions[0].EquivalenceKey).IsEqualTo(Key);
        await Assert.That(await ApplyAsync(actions[0], document)).IsEqualTo(SecondRenamed);
    }

    /// <summary>Verifies a semantically try-resolved rewrite registration offers only resolved targets and replaces the target with its rewrite on apply.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SemanticRewriteRegistrationReplacesTheResolvedTargetAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace);
        var actions = await CollectAsync(
            document,
            static context => TargetCodeFix.RegisterAsync(
                context,
                Title,
                Key,
                static (SyntaxNode root, SemanticModel _, Diagnostic diagnostic, CancellationToken _, [MaybeNullWhen(false)] out ExpressionStatementSyntax statement) =>
                    (statement = ResolveSecondStatement(root, diagnostic)!) is not null,
                RenameCall));

        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(await ApplyAsync(actions[0], document)).IsEqualTo(SecondRenamed);
    }

    /// <summary>Verifies applying a rewrite replaces the target node and leaves the rest of the document untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ApplyReplacesTheTargetWithItsRewriteAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = CreateDocument(workspace);
        var root = (await document.GetSyntaxRootAsync())!;
        var first = root.DescendantNodes().OfType<ExpressionStatementSyntax>().First();

        var changed = TargetCodeFix.Apply(document, root, first, RenameCall);

        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(FirstRenamed);
    }

    /// <summary>Creates a document holding the fixture source.</summary>
    /// <param name="workspace">The workspace owning the document.</param>
    /// <returns>The document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Document CreateDocument(AdhocWorkspace workspace) =>
        workspace.AddProject(nameof(TargetCodeFixTests), LanguageNames.CSharp).AddDocument(DocumentName, Source);

    /// <summary>Runs a registration over one diagnostic per statement and collects the code actions it offers.</summary>
    /// <param name="document">The document the diagnostics belong to.</param>
    /// <param name="register">The registration under test.</param>
    /// <param name="extraFirstStatementRule">An additional rule reported on the first statement, when set.</param>
    /// <returns>The offered code actions, in registration order.</returns>
    private static async Task<List<CodeAction>> CollectAsync(Document document, Func<CodeFixContext, Task> register, DiagnosticDescriptor? extraFirstStatementRule = null)
    {
        var root = (await document.GetSyntaxRootAsync())!;
        var builder = ImmutableArray.CreateBuilder<Diagnostic>();
        builder.Add(Report(Rule, root, FirstCall));
        builder.Add(Report(OtherRule, root, SecondCall));
        if (extraFirstStatementRule is not null)
        {
            builder.Add(Report(extraFirstStatementRule, root, FirstCall));
        }

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(document, root.Span, builder.ToImmutable(), (action, _) => actions.Add(action), CancellationToken.None);
        await register(context);
        return actions;
    }

    /// <summary>Creates a diagnostic over the whole document that names the call it reports, since a code fix context shares one span.</summary>
    /// <param name="rule">The reported rule.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="call">The reported call.</param>
    /// <returns>The diagnostic.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Diagnostic Report(DiagnosticDescriptor rule, SyntaxNode root, string call) =>
        Diagnostic.Create(rule, root.GetLocation(), ImmutableDictionary<string, string?>.Empty.Add(CallProperty, call));

    /// <summary>Applies a code action and returns the changed document's text.</summary>
    /// <param name="action">The code action.</param>
    /// <param name="document">The document the action edits.</param>
    /// <returns>The changed text.</returns>
    private static async Task<string> ApplyAsync(CodeAction action, Document document)
    {
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        return (await changed.GetTextAsync()).ToString();
    }

    /// <summary>Returns the title a diagnostic-titled registration uses, leaving the first rule untitled.</summary>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The descriptor title, or <see langword="null"/> for the first rule.</returns>
    private static string? TitleForOtherRule(Diagnostic diagnostic) =>
        diagnostic.Id == Rule.Id ? null : diagnostic.Descriptor.Title.ToString();

    /// <summary>Returns whether a diagnostic reports the second statement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns><see langword="true"/> when the reported statement calls <c>B</c>.</returns>
    private static bool IsSecondStatement(SyntaxNode root, Diagnostic diagnostic) =>
        Statement(root, diagnostic).Expression.ToString() == SecondCall;

    /// <summary>Returns the statement a diagnostic reports.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The reported statement.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionStatementSyntax Statement(SyntaxNode root, Diagnostic diagnostic) =>
        root.DescendantNodes().OfType<ExpressionStatementSyntax>().First(statement => statement.Expression.ToString() == diagnostic.Properties[CallProperty]);

    /// <summary>Renames the call in a statement to <c>Z()</c>.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="statement">The statement to rename.</param>
    /// <returns>The updated root.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SyntaxNode Rename(SyntaxNode root, ExpressionStatementSyntax statement) =>
        root.ReplaceNode(statement.Expression, SyntaxFactory.ParseExpression("Z()").WithTriviaFrom(statement.Expression));

    /// <summary>Resolves the statement a diagnostic reports when it is the second statement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The second statement, or <see langword="null"/> for any other diagnostic.</returns>
    private static ExpressionStatementSyntax? ResolveSecondStatement(SyntaxNode root, Diagnostic diagnostic) =>
        IsSecondStatement(root, diagnostic) ? Statement(root, diagnostic) : null;

    /// <summary>Rewrites a statement so it calls <c>Z()</c>.</summary>
    /// <param name="statement">The statement to rewrite.</param>
    /// <returns>The rewritten statement.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionStatementSyntax RenameCall(ExpressionStatementSyntax statement) =>
        statement.WithExpression(SyntaxFactory.ParseExpression("Z()").WithTriviaFrom(statement.Expression));

    /// <summary>Renames the call in the statement a diagnostic reports.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>The updated document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Document RenameReported(Document document, SyntaxNode root, Diagnostic diagnostic) =>
        document.WithSyntaxRoot(Rename(root, Statement(root, diagnostic)));

    /// <summary>Renames the call in the statement a diagnostic reports, reading the root from the document.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> RenameReportedAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var root = (await document.GetSyntaxRootAsync(cancellationToken))!;
        return RenameReported(document, root, diagnostic);
    }
}
