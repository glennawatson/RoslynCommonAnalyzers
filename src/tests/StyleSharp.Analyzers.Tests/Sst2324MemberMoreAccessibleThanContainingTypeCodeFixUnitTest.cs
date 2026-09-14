// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using VerifyNarrowAccess = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2324MemberMoreAccessibleThanContainingTypeAnalyzer,
    StyleSharp.Analyzers.Sst2324MemberMoreAccessibleThanContainingTypeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST2324 code fix (narrow a member to its container's accessibility).</summary>
public class Sst2324MemberMoreAccessibleThanContainingTypeCodeFixUnitTest
{
    /// <summary>Verifies both edit paths preserve leading comments and non-access modifiers while replacing accessibility.</summary>
    /// <param name="modifiers">The original method modifiers, including their trailing space.</param>
    /// <param name="target">The accessibility stored on the diagnostic.</param>
    /// <param name="expectedModifiers">The resulting method modifiers, including their trailing space.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public ", "internal", "internal ")]
    [Arguments("protected internal ", "private protected", "private protected ")]
    [Arguments("private protected ", "protected", "protected ")]
    [Arguments("internal ", "public", "public ")]
    [Arguments("static public ", "private", "static private ")]
    [Arguments("public static ", "unknown", "static ")]
    [Arguments("public ", "unknown internal", "internal ")]
    [Arguments("public ", " internal ", "internal ")]
    [Arguments("public ", "public", "public ")]
    public async Task TargetKeywordsReplaceAccessModifiersAsync(string modifiers, string target, string expectedModifiers)
    {
        var source = $"class C {{\n    // Member\n    {modifiers}void M() {{ }}\n}}";
        var expected = $"class C {{\n    // Member\n    {expectedModifiers}void M() {{ }}\n}}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var properties = ImmutableDictionary<string, string?>.Empty.Add(Sst2324MemberMoreAccessibleThanContainingTypeAnalyzer.TargetAccessibilityKey, target);
        var diagnostic = Diagnostic.Create(DesignRules.MemberMoreAccessibleThanContainingType, method.Modifiers[0].GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst2324MemberMoreAccessibleThanContainingTypeCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        await Assert.That((await changed.GetTextAsync()).ToString()).IsEqualTo(expected);
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2324MemberMoreAccessibleThanContainingTypeCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(expected);
    }

    /// <summary>Verifies stale locations and unusable accessibility metadata register neither an action nor a batch edit.</summary>
    /// <param name="source">The current document receiving the diagnostic.</param>
    /// <param name="expectedText">The text at the diagnostic location.</param>
    /// <param name="target">The target accessibility metadata, which may be null or empty.</param>
    /// <param name="hasTarget">Whether the diagnostic includes the target property.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { public void M() { } }", "public", "internal", false)]
    [Arguments("class C { public void M() { } }", "public", null, true)]
    [Arguments("class C { public void M() { } }", "public", "", true)]
    [Arguments("class C { public void M() { return; } }", "return", "internal", true)]
    [Arguments("class C { void M() { } }", "M", "internal", true)]
    [Arguments("class C { static void M() { } }", "static", "internal", true)]
    [Arguments("class C { public void M() { } }", "public", "unknown", true)]
    [Arguments("class C { public void M() { } }", "public", "unknown missing", true)]
    [Arguments("class C { public void M() { } }", "public", " ", true)]
    public async Task InapplicableDiagnosticLeavesDocumentUnchangedAsync(string source, string expectedText, string? target, bool hasTarget)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var token = root.DescendantTokens().Single(candidate => candidate.ValueText == expectedText);
        var properties = hasTarget
            ? ImmutableDictionary<string, string?>.Empty.Add(Sst2324MemberMoreAccessibleThanContainingTypeAnalyzer.TargetAccessibilityKey, target)
            : ImmutableDictionary<string, string?>.Empty;
        var diagnostic = Diagnostic.Create(DesignRules.MemberMoreAccessibleThanContainingType, token.GetLocation(), properties);
        using var container = new ContainerConfiguration().WithPart<Sst2324MemberMoreAccessibleThanContainingTypeCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst2324MemberMoreAccessibleThanContainingTypeCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies an empty keyword list cannot remove a member's sole modifier.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyAccessibilityPreservesOriginalDeclarationAsync()
    {
        var declaration = SyntaxFactory.ParseMemberDeclaration("public void M() { }")!;
        var rewritten = Sst2324MemberMoreAccessibleThanContainingTypeCodeFixProvider.WithAccessibility(declaration, string.Empty);
        await Assert.That(ReferenceEquals(rewritten, declaration)).IsTrue();
    }

    /// <summary>Verifies the analyzer's restricted container accessibility becomes the method's new modifiers.</summary>
    /// <param name="containerAccess">The enclosing nested class's accessibility.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("protected")]
    [Arguments("protected internal")]
    [Arguments("private protected")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NestedContainerAccessibilityBecomesMethodAccessibilityAsync(string containerAccess) =>
        VerifyNarrowAccess.VerifyCodeFixAsync(
            $$"""
            public class Outer
            {
                {{containerAccess}} class Inner
                {
                    {|SST2324:public|} void M() { }
                }
            }
            """,
            $$"""
            public class Outer
            {
                {{containerAccess}} class Inner
                {
                    {{containerAccess}} void M() { }
                }
            }
            """);

    /// <summary>Verifies both old accessibility tokens are replaced by the internal container's accessibility.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ProtectedInternalMethodBecomesInternalAsync() =>
        VerifyNarrowAccess.VerifyCodeFixAsync(
            """
            internal class C
            {
                {|SST2324:protected|} internal void M() { }
            }
            """,
            """
            internal class C
            {
                internal void M() { }
            }
            """);

    /// <summary>Verifies a public method in an internal type becomes internal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMethodInInternalTypeBecomesInternalAsync()
    {
        const string Source = """
                              internal class Container
                              {
                                  {|SST2324:public|} void Method()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class Container
                                   {
                                       internal void Method()
                                       {
                                       }
                                   }
                                   """;
        await VerifyNarrowAccess.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a nested type keeps the modifiers that carry no accessibility.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherModifiersSurviveTheNarrowingAsync()
    {
        const string Source = """
                              internal class Container
                              {
                                  {|SST2324:public|} static class Nested
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class Container
                                   {
                                       internal static class Nested
                                       {
                                       }
                                   }
                                   """;
        await VerifyNarrowAccess.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the member's documentation stays above the narrowed modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DocumentationStaysAboveTheMemberAsync()
    {
        const string Source = """
                              internal class Container
                              {
                                  /// <summary>Does a thing.</summary>
                                  {|SST2324:public|} void Method()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class Container
                                   {
                                       /// <summary>Does a thing.</summary>
                                       internal void Method()
                                       {
                                       }
                                   }
                                   """;
        await VerifyNarrowAccess.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a member of a private nested type is narrowed to private.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberOfPrivateNestedTypeBecomesPrivateAsync()
    {
        const string Source = """
                              public class Host
                              {
                                  private sealed class Harness
                                  {
                                      {|SST2324:public|} void Method()
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class Host
                                   {
                                       private sealed class Harness
                                       {
                                           private void Method()
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifyNarrowAccess.VerifyCodeFixAsync(Source, FixedSource);
    }
}
