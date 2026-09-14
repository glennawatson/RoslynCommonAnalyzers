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
using Microsoft.CodeAnalysis.Editing;
using VerifyCombination = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2330FlagsCombinationLiteralAnalyzer,
    StyleSharp.Analyzers.Sst2330FlagsCombinationLiteralCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2330FlagsCombinationLiteralCodeFixProvider"/> (SST2330 name the combined flags).</summary>
public class Sst2330FlagsCombinationLiteralCodeFixUnitTest
{
    /// <summary>A three-bit combination written as the literal it adds up to.</summary>
    private const string ThreeBitSource = """
        using System;

        [Flags]
        public enum Access
        {
            None = 0,
            Read = 1,
            Write = 2,
            Execute = 4,
            All = {|SST2330:7|},
        }
        """;

    /// <summary>The three-bit combination after the fix names its members.</summary>
    private const string ThreeBitFixed = """
        using System;

        [Flags]
        public enum Access
        {
            None = 0,
            Read = 1,
            Write = 2,
            Execute = 4,
            All = Read | Write | Execute,
        }
        """;

    /// <summary>A two-bit combination written as a literal.</summary>
    private const string TwoBitSource = """
        using System;

        [Flags]
        public enum Access
        {
            None = 0,
            Read = 1,
            Write = 2,
            ReadWrite = {|SST2330:3|},
        }
        """;

    /// <summary>The two-bit combination after the fix.</summary>
    private const string TwoBitFixed = """
        using System;

        [Flags]
        public enum Access
        {
            None = 0,
            Read = 1,
            Write = 2,
            ReadWrite = Read | Write,
        }
        """;

    /// <summary>Verifies the fix rewrites the literal into the OR of every member it combines.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RewritesLiteralToMemberOrAsync() =>
        VerifyCombination.VerifyCodeFixAsync(ThreeBitSource, ThreeBitFixed);

    /// <summary>Verifies the fix names two members for a two-bit combination.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RewritesTwoBitLiteralAsync() =>
        VerifyCombination.VerifyCodeFixAsync(TwoBitSource, TwoBitFixed);

    /// <summary>Verifies stale member properties and qualified names follow the same single and batch edit paths.</summary>
    /// <param name="expression">The enum value at the diagnostic.</param>
    /// <param name="members">The member-list property, or null when absent.</param>
    /// <param name="expected">The replacement value, or null when no edit applies.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("3", null, null)]
    [Arguments("3", "", null)]
    [Arguments("3", ",", null)]
    [Arguments("A", "A,B", null)]
    [Arguments("3", "Access.A,Access.B", "Access.A | Access.B")]
    [Arguments("3", "@", "")]
    [Arguments("3", "@class,B", "@class | B")]
    public async Task MemberPropertyControlsFixAsync(string expression, string? members, string? expected)
    {
        var source = $"enum Access {{ A = 1, B = 2, Both = {expression} }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var value = root.DescendantNodes().OfType<EnumMemberDeclarationSyntax>().Last().EqualsValue!.Value;
        var properties = members is null ? ImmutableDictionary<string, string?>.Empty : ImmutableDictionary<string, string?>.Empty.Add(Sst2330FlagsCombinationLiteralAnalyzer.MembersKey, members);
        var diagnostic = Diagnostic.Create(DesignRules.FlagsCombinationLiteralShouldNameMembers, value.GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst2330FlagsCombinationLiteralCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(expected is null ? 0 : 1);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        var expectedSource = $"enum Access {{ A = 1, B = 2, Both = {expected ?? expression} }}";
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
