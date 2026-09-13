// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;
using VerifyReadonlyLock = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.LockTargetAnalyzer,
    StyleSharp.Analyzers.Sst1904ReadonlyLockFieldCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1904ReadonlyLockFieldCodeFixProvider"/> (SST1904 make the lock field readonly).</summary>
public class Sst1904ReadonlyLockFieldCodeFixUnitTest
{
    /// <summary>A non-readonly private object field locked on.</summary>
    private const string ModifiedFieldSource = """
        public class C
        {
            private object _gate = new();

            public void M()
            {
                lock ({|SST1904:_gate|})
                {
                }
            }
        }
        """;

    /// <summary>The field after the fix.</summary>
    private const string ModifiedFieldFixed = """
        public class C
        {
            private readonly object _gate = new();

            public void M()
            {
                lock (_gate)
                {
                }
            }
        }
        """;

    /// <summary>A field with no modifiers locked on.</summary>
    private const string NoModifierFieldSource = """
        public class C
        {
            object _gate = new();

            public void M()
            {
                lock ({|SST1904:_gate|})
                {
                }
            }
        }
        """;

    /// <summary>The no-modifier field after the fix.</summary>
    private const string NoModifierFieldFixed = """
        public class C
        {
            readonly object _gate = new();

            public void M()
            {
                lock (_gate)
                {
                }
            }
        }
        """;

    /// <summary>Verifies the fix inserts <c>readonly</c> after the access modifier.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MakesAModifiedFieldReadonlyAsync() =>
        VerifyReadonlyLock.VerifyCodeFixAsync(ModifiedFieldSource, ModifiedFieldFixed);

    /// <summary>Verifies the fix adds <c>readonly</c> to a field that had no modifiers.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MakesANoModifierFieldReadonlyAsync() =>
        VerifyReadonlyLock.VerifyCodeFixAsync(NoModifierFieldSource, NoModifierFieldFixed);

    /// <summary>Checks unsafe or stale lock targets do not offer or apply a readonly edit.</summary>
    /// <param name="members">The field declarations.</param>
    /// <param name="body">The method body containing the target.</param>
    /// <param name="target">The diagnostic target text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("object gate = new(), other = new();", "lock (gate) { }", "gate")]
    [Arguments("readonly object gate = new();", "lock (gate) { }", "gate")]
    [Arguments("object gate = new();", "gate = new(); lock (gate) { }", "gate")]
    [Arguments("object gate = new();", "this.gate = new(); lock (gate) { }", "gate")]
    [Arguments("object gate = new();", "(gate) = new(); lock (gate) { }", "gate")]
    [Arguments("object Gate => new();", "lock (Gate) { }", "Gate")]
    [Arguments("", "lock (missing) { }", "missing")]
    public async Task InapplicableLockTargetIsUnchangedAsync(string members, string body, string target)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("LockTarget", LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument("Test.cs", $"class C {{ {members} void M() {{ {body} }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var lockStatement = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.LockStatementSyntax>().Single();
        var diagnostic = Diagnostic.Create(ConcurrencyRules.DoNotLockOnNonReadonlyField, lockStatement.Expression.GetLocation(), target);
        using var container = new ContainerConfiguration().WithPart<Sst1904ReadonlyLockFieldCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Checks constructor writes and writes to other fields permit the readonly edit.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConstructorAndUnrelatedWritesPermitReadonlyAsync() =>
        VerifyReadonlyLock.VerifyCodeFixAsync(
            """
            class C
            {
                private object gate;
                private object other;
                public C() { gate = new(); }
                void M(C c)
                {
                    other = new();
                    this.other = new();
                    object gate = new();
                    gate = new();
                    lock ({|SST1904:this.gate|}) { }
                }
            }
            """,
            """
            class C
            {
                private readonly object gate;
                private object other;
                public C() { gate = new(); }
                void M(C c)
                {
                    other = new();
                    this.other = new();
                    object gate = new();
                    gate = new();
                    lock (this.gate) { }
                }
            }
            """);
}
