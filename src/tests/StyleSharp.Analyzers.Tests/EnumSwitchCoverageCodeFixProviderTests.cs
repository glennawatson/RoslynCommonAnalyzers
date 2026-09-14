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
using VerifySwitch = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.EnumSwitchCoverageAnalyzer,
    StyleSharp.Analyzers.EnumSwitchCoverageCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests enum-switch fixes with incomplete diagnostics and multiple missing arms.</summary>
public class EnumSwitchCoverageCodeFixProviderTests
{
    /// <summary>The name of the document receiving a diagnostic.</summary>
    private const string FileName = "Test.cs";

    /// <summary>A document with no switch for stale diagnostic tests.</summary>
    private const string EmptyClassSource = "class C { }";

    /// <summary>The enum value encoded in a saved diagnostic.</summary>
    private const string MissingMember = "E.B";

    /// <summary>Verifies absent and empty member payloads leave the document unchanged.</summary>
    /// <param name="includeProperty">Whether the payload property is present.</param>
    /// <param name="members">The encoded missing members.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false, null)]
    [Arguments(true, null)]
    [Arguments(true, "")]
    [Arguments(true, " \t ")]
    public async Task MissingMemberPayloadDoesNotChangeTheDocumentAsync(bool includeProperty, string? members)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("MissingPayload", LanguageNames.CSharp).AddDocument(FileName, EmptyClassSource);
        var root = (await document.GetSyntaxRootAsync())!;
        var properties = includeProperty
            ? ImmutableDictionary<string, string?>.Empty.Add(EnumSwitchCoverageAnalyzer.MissingMembersProperty, members)
            : ImmutableDictionary<string, string?>.Empty;
        var diagnostic = Diagnostic.Create(ModernSyntaxRules.CompleteEnumSwitchStatement, root.GetLocation(), properties);

        await Assert.That(EnumSwitchCoverageCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
        if (includeProperty)
        {
            return;
        }

        using var container = new ContainerConfiguration().WithPart<EnumSwitchCoverageCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(0);
    }

    /// <summary>Verifies a stale diagnostic outside a switch cannot insert cases or arms.</summary>
    /// <param name="expression">Whether the diagnostic requests expression arms.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MissingSwitchLeavesTheDocumentUnchangedAsync(bool expression)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("MissingSwitch", LanguageNames.CSharp).AddDocument(FileName, EmptyClassSource);
        var root = (await document.GetSyntaxRootAsync())!;
        var descriptor = expression ? ModernSyntaxRules.CompleteEnumSwitchExpression : ModernSyntaxRules.CompleteEnumSwitchStatement;
        var diagnostic = Diagnostic.Create(
            descriptor,
            root.GetLocation(),
            ImmutableDictionary<string, string?>.Empty.Add(EnumSwitchCoverageAnalyzer.MissingMembersProperty, MissingMember));

        await Assert.That(EnumSwitchCoverageCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
    }

    /// <summary>Verifies an unrelated diagnostic cannot select an enum-switch rewrite.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnrelatedDiagnosticLeavesTheDocumentUnchangedAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("UnrelatedDiagnostic", LanguageNames.CSharp).AddDocument(FileName, EmptyClassSource);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(
            ReadabilityRules.ElementsConsistentIndentation,
            root.GetLocation(),
            ImmutableDictionary<string, string?>.Empty.Add(EnumSwitchCoverageAnalyzer.MissingMembersProperty, MissingMember));

        await Assert.That(EnumSwitchCoverageCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
    }

    /// <summary>Verifies applying a saved diagnostic respects directives inside its switch.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StatementWithDirectivesRejectsDirectApplicationAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("SwitchDirective", LanguageNames.CSharp).AddDocument(
            FileName,
            "class C { void M(int value) { switch (value) {\n#region Cases\ncase 0: break;\n#endregion\n} } }");
        var root = (await document.GetSyntaxRootAsync())!;
        var statement = root.DescendantNodes().OfType<SwitchStatementSyntax>().Single();
        var diagnostic = Diagnostic.Create(
            ModernSyntaxRules.CompleteEnumSwitchStatement,
            statement.SwitchKeyword.GetLocation(),
            ImmutableDictionary<string, string?>.Empty.Add(EnumSwitchCoverageAnalyzer.MissingMembersProperty, "1"));

        await Assert.That(EnumSwitchCoverageCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
    }

    /// <summary>Verifies stale catch-all actions preserve missing switches and directive boundaries.</summary>
    /// <param name="source">The source at the saved diagnostic span.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(EmptyClassSource)]
    [Arguments("class C { void M(int value) { switch (value) {\n#region Cases\ncase 0: break;\n#endregion\n} } }")]
    public async Task CatchAllAtInapplicableSpanPreservesTheDocumentAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("MissingCatchAllSwitch", LanguageNames.CSharp).AddDocument(FileName, source);
        var root = (await document.GetSyntaxRootAsync())!;
        var start = source.IndexOf("switch", StringComparison.Ordinal);
        var location = start < 0 ? root.GetLocation() : root.SyntaxTree.GetLocation(TextSpan.FromBounds(start, root.Span.End));
        var diagnostic = Diagnostic.Create(
            ModernSyntaxRules.CompleteEnumSwitchStatement,
            location,
            ImmutableDictionary<string, string?>.Empty.Add(EnumSwitchCoverageAnalyzer.CatchAllProperty, "true"));
        using var container = new ContainerConfiguration().WithPart<EnumSwitchCoverageCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));

        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var updated = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await updated.GetTextAsync()).ToString()).IsEqualTo((await document.GetTextAsync()).ToString());
        await Assert.That(provider.GetFixAllProvider()).IsSameReferenceAs(WellKnownFixAllProviders.BatchFixer);
    }

    /// <summary>Verifies directives within an expression prevent action registration.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExpressionWithDirectivesHasNoFixAsync() =>
        VerifySwitch.VerifyCodeFixAsync(
            """
            enum E { A, B }
            class C
            {
                int M(E value) => value {|SST2206:switch|}
                {
            #region Arms
                    E.A => 1
            #endregion
                };
            }
            """,
            """
            enum E { A, B }
            class C
            {
                int M(E value) => value {|SST2206:switch|}
                {
            #region Arms
                    E.A => 1
            #endregion
                };
            }
            """);

    /// <summary>Verifies every encoded missing member becomes a separate throwing arm.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MultipleMissingMembersBecomeSeparateArmsAsync() =>
        VerifySwitch.VerifyCodeFixAsync(
            """
            enum E { A, B, C }
            class C
            {
                int M(E value) => value {|SST2206:switch|}
                {
                    E.A => 1
                };
            }
            """,
            """
            enum E { A, B, C }
            class C
            {
                int M(E value) => value switch
                {
                    E.A => 1,
                    E.B => throw new global::System.NotImplementedException(),
                    E.C => throw new global::System.NotImplementedException()
                };
            }
            """);
}
