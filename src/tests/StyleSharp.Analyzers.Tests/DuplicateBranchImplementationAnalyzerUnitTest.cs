// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;
using VerifyBranches = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.IdenticalBranchesAnalyzer>;
using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.IdenticalBranchesAnalyzer,
    StyleSharp.Analyzers.Sst2414DuplicateBranchImplementationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2414 (two branches of one conditional share an implementation).</summary>
public class DuplicateBranchImplementationAnalyzerUnitTest
{
    /// <summary>The document name used for direct fix-provider tests.</summary>
    private const string DocumentName = "Test.cs";

    /// <summary>A switch statement whose first and third sections share a body.</summary>
    private const string DuplicateSectionSource = """
        public sealed class C
        {
            public int M(int x)
            {
                switch (x)
                {
                    case 1:
                        return A();
                    case 2:
                        return B();
                    {|SST2414:case 3:|}
                        return A();
                }

                return 0;
            }

            private static int A() => 1;

            private static int B() => 2;
        }
        """;

    /// <summary>The switch after the two sections are merged.</summary>
    private const string DuplicateSectionFixed = """
        public sealed class C
        {
            public int M(int x)
            {
                switch (x)
                {
                    case 1:
                    case 3:
                        return A();
                    case 2:
                        return B();
                }

                return 0;
            }

            private static int A() => 1;

            private static int B() => 2;
        }
        """;

    /// <summary>Verifies stale section diagnostics are rejected by single and batch fixes.</summary>
    /// <param name="sections">The switch sections after the diagnostic became stale.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("case 1: return 1;")]
    [Arguments("case 1: return 1; case 2: return 2;")]
    [Arguments("case 1: M(); return 1; case 2: return 1;")]
    [Arguments("case 1: return 1;\n#if UNUSED\ncase 9: return 9;\n#endif\ncase 2: return 1;")]
    public async Task UnmergeableSectionHasNoFixAsync(string sections)
    {
        var source = $"class C {{ int M(int x = 0) {{ switch (x) {{ {sections} }} return 0; }} }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var section = root.DescendantNodes().OfType<SwitchSectionSyntax>().Last();
        await VerifyNoFixAsync(document, section.GetLocation());
    }

    /// <summary>Verifies stale arm diagnostics reject first arms, guards, mismatched values, and directives.</summary>
    /// <param name="arms">The switch-expression arms after the diagnostic became stale.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("1 => 1")]
    [Arguments("1 => 1, 2 when x > 0 => 1")]
    [Arguments("1 when x > 0 => 1, 2 => 1")]
    [Arguments("1 => 1, 2 => 2")]
    [Arguments("1 => 1,\n#if UNUSED\n9 => 9,\n#endif\n2 => 1")]
    public async Task UnmergeableArmHasNoFixAsync(string arms)
    {
        var source = $"class C {{ int M(int x) => x switch {{ {arms} }}; }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var arm = root.DescendantNodes().OfType<SwitchExpressionArmSyntax>().Last();
        await VerifyNoFixAsync(document, arm.Pattern.GetLocation());
    }

    /// <summary>Verifies a diagnostic outside a switch is ignored by both fix paths.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticOutsideSwitchHasNoFixAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From("class C { }"));
        var root = (await document.GetSyntaxRootAsync())!;
        await VerifyNoFixAsync(document, root.GetLocation());
    }

    /// <summary>Verifies an incomplete earlier section cannot be selected as a merge partner.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyEarlierSectionHasNoFixAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, DocumentName, SourceText.From("class C { int M(int x) { switch (x) { case 1: return 1; case 2: return 1; } return 0; } }"));
        var root = (await document.GetSyntaxRootAsync())!;
        var first = root.DescendantNodes().OfType<SwitchSectionSyntax>().First();
        document = document.WithSyntaxRoot(root.ReplaceNode(first, first.WithStatements(default)));
        root = (await document.GetSyntaxRootAsync())!;
        await VerifyNoFixAsync(document, root.DescendantNodes().OfType<SwitchSectionSyntax>().Last().GetLocation());
    }

    /// <summary>Verifies a later matching arm is found after guarded and unequal candidates.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MergeSkipsEarlierIncompatibleArmsAsync() =>
        VerifyFix.VerifyCodeFixAsync(
            "class C { int M(int x) => x switch { 1 when x > 0 => 1, 2 => 2, 3 or 4 => 1, {|SST2414:5|} => 1, _ => 0 }; }",
            "class C { int M(int x) => x switch { 1 when x > 0 => 1, 2 => 2, 3 or 4 or 5 => 1, _ => 0 }; }");

    /// <summary>Verifies an and-pattern keeps its precedence when it becomes an or alternative.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task JoinedAndPatternKeepsItsPrecedenceAsync() =>
        VerifyFix.VerifyCodeFixAsync(
            "class C { int M(int x) => x switch { 1 and > 0 => 1, 2 => 2, {|SST2414:3|} => 1, _ => 0 }; }",
            "class C { int M(int x) => x switch { 1 and > 0 or 3 => 1, 2 => 2, _ => 0 }; }");

    /// <summary>Verifies sections of different lengths and bodies are skipped before the matching section.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MergeSkipsEarlierIncompatibleSectionsAsync() =>
        VerifyFix.VerifyCodeFixAsync(
            "class C { int M(int x) { switch (x) { case 1: M(0); return 1; case 2: return 2; case 3: return 1; {|SST2414:case 4:|} return 1; } return 0; } }",
            "class C { int M(int x) { switch (x) { case 1: M(0); return 1; case 2: return 2; case 3: case 4: return 1; } return 0; } }");

    /// <summary>Verifies two switch sections with the same body are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DuplicateSectionIsReportedAsync() =>
        VerifyBranches.VerifyAnalyzerAsync(DuplicateSectionSource);

    /// <summary>Verifies two switch-expression arms with the same value are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DuplicateArmIsReportedAsync() =>
        VerifyBranches.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(int x) => x switch
                {
                    1 => "a",
                    2 => "b",
                    {|SST2414:3|} => "a",
                    _ => "z",
                };
            }
            """);

    /// <summary>Verifies non-adjacent sections with pattern labels are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Merging stacks the later labels onto the earlier section, lifting them up the switch. A broad type
    /// pattern moved above a narrower one leaves that one unreachable.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonAdjacentPatternSectionsAreCleanAsync() =>
        VerifyBranches.VerifyAnalyzerAsync(
            """
            public class Animal
            {
            }

            public sealed class Dog : Animal
            {
            }

            public sealed class C
            {
                public string M(object value)
                {
                    switch (value)
                    {
                        case string:
                            return "none";
                        case Dog:
                            return "dog";
                        case Animal:
                            return "none";
                    }

                    return "other";
                }
            }
            """);

    /// <summary>Verifies sections whose labels declare a name are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Stacking two labels that each declare the same name does not compile (CS0128), and neither does the
    /// or pattern the stack later folds into (CS8780).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SectionsDeclaringANameAreCleanAsync() =>
        VerifyBranches.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object value, out ulong bits)
                {
                    switch (value)
                    {
                        case int number:
                            bits = (ulong)number;
                            return true;
                        case short number:
                            bits = (ulong)number;
                            return true;
                    }

                    bits = 0;
                    return false;
                }
            }
            """);

    /// <summary>Verifies two switch-expression arms with the same value are joined with an or pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DuplicateArmIsJoinedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(int x) => x switch
                                  {
                                      1 => "a",
                                      2 => "b",
                                      {|SST2414:3|} => "a",
                                      _ => "z",
                                  };
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(int x) => x switch
                                       {
                                           1 or 3 => "a",
                                           2 => "b",
                                           _ => "z",
                                       };
                                   }
                                   """;
        await VerifyFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a joined arm too wide for one line puts each alternative on its own.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task JoinedArmTooWideForOneLineIsWrappedAsync()
    {
        const string Config = """
            root = true
            [*.cs]
            stylesharp.max_line_length = 60

            """;
        var test = new VerifyFix.Test
        {
            TestCode = """
                       public sealed class C
                       {
                           public string M(string text) => text switch
                           {
                               "the first alternative" => "matched",
                               {|SST2414:"the second alternative"|} => "matched",
                               _ => "z",
                           };
                       }
                       """,
            FixedCode = """
                        public sealed class C
                        {
                            public string M(string text) => text switch
                            {
                                "the first alternative"
                                    or "the second alternative" => "matched",
                                _ => "z",
                            };
                        }
                        """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", Config));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies duplicate arms separated by a directive are reported but not joined.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The duplicate carries the <c>#endif</c> as its leading trivia, so joining it into the partner takes
    /// the close and leaves the <c>#if</c> open — and the pattern it contributes is one the condition no
    /// longer covers.
    /// </remarks>
    [Test]
    public async Task DuplicateArmAcrossADirectiveIsNotJoinedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(int x) => x switch
                                  {
                                      1 => "a",
                              #if LEGACY
                                      9 => "legacy",
                              #endif
                                      {|SST2414:3|} => "a",
                                      _ => "z",
                                  };
                              }
                              """;
        await VerifyFix.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies two if-chain branches with the same multi-statement body are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DuplicateIfBranchIsReportedAsync() =>
        VerifyBranches.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int x, Action a, Action b)
                {
                    if (x == 1)
                    {
                        a();
                        b();
                    }
                    else if (x == 2)
                    {
                        b();
                        a();
                    }
                    else {|SST2414:if|} (x == 3)
                    {
                        a();
                        b();
                    }
                }
            }
            """);

    /// <summary>Verifies a case that shares its body with the default section is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CaseSharingDefaultBodyIsCleanAsync() =>
        VerifyBranches.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int x)
                {
                    switch (x)
                    {
                        case 1:
                            return A();
                        case 2:
                            return B();
                        default:
                            return A();
                    }
                }

                private static int A() => 1;

                private static int B() => 2;
            }
            """);

    /// <summary>Verifies a switch a goto could jump into is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SwitchWithGotoIsCleanAsync() =>
        VerifyBranches.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int x)
                {
                    switch (x)
                    {
                        case 0:
                            goto case 1;
                        case 1:
                            return A();
                        case 2:
                            return B();
                        case 3:
                            return A();
                    }

                    return 0;
                }

                private static int A() => 1;

                private static int B() => 2;
            }
            """);

    /// <summary>Verifies single-statement if branches are below the reporting threshold.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleStatementIfBranchesAreCleanAsync() =>
        VerifyBranches.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int x, Action a, Action b)
                {
                    if (x == 1)
                    {
                        a();
                    }
                    else if (x == 2)
                    {
                        b();
                    }
                    else if (x == 3)
                    {
                        a();
                    }
                }
            }
            """);

    /// <summary>Verifies a fully duplicated switch is the SST1476 case, not SST2414.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FullyDuplicatedSwitchIsTheAllBranchesCaseAsync() =>
        VerifyBranches.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int x)
                {
                    {|SST1476:switch|} (x)
                    {
                        case 1:
                            return A();
                        case 2:
                            return A();
                        default:
                            return A();
                    }
                }

                private static int A() => 1;
            }
            """);

    /// <summary>Verifies the fix stacks the duplicated section's labels onto the earlier one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixMergesDuplicateSectionsAsync() =>
        VerifyFix.VerifyCodeFixAsync(DuplicateSectionSource, DuplicateSectionFixed);

    /// <summary>Verifies arms that bind a name are left alone, since an <c>or</c> cannot join them.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Reading the same member off two different shapes is the only way to write this, so the repeated
    /// result says nothing about a mistake.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArmsBindingANameAreCleanAsync() =>
        VerifyBranches.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public bool M(object value, string wanted) => value switch
                {
                    System.Text.StringBuilder { Length: var length } => length == wanted.Length,
                    string { Length: var length } => length == wanted.Length,
                    _ => false,
                };
            }
            """);

    /// <summary>Verifies arms an <c>or</c> pattern could join are still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CombinableArmsAreStillReportedAsync() =>
        VerifyBranches.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int x) => x switch
                {
                    1 => A(),
                    2 => B(),
                    {|SST2414:3|} => A(),
                    _ => 0,
                };

                private static int A() => 1;

                private static int B() => 2;
            }
            """);

    /// <summary>Checks that registration offers nothing and a batch edit retains the original syntax.</summary>
    /// <param name="document">The document containing the stale diagnostic.</param>
    /// <param name="location">The diagnostic location.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    private static async Task VerifyNoFixAsync(Document document, Location location)
    {
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(CorrectnessRules.DuplicateBranchImplementation, location);
        using var container = new ContainerConfiguration().WithPart<Sst2414DuplicateBranchImplementationCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }
}
