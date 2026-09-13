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
using RoslynCommon.Analyzers.Tests;

using AnalyzerVerify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1016UseBitwiseFlagTestAnalyzer>;
using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1016UseBitwiseFlagTestAnalyzer,
    PerformanceSharp.Analyzers.Psh1016UseBitwiseFlagTestCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1016UseBitwiseFlagTestAnalyzer"/> (PSH1016 bitwise flag test).</summary>
public class UseBitwiseFlagTestAnalyzerUnitTest
{
    /// <summary>Verifies a HasFlag call is flagged and rewritten to a bitwise test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasFlagIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              [Flags]
                              public enum MyFlags
                              {
                                  None = 0,
                                  A = 1,
                                  B = 2,
                              }

                              public class C
                              {
                                  public bool M(MyFlags flags) => {|PSH1016:flags.HasFlag(MyFlags.A)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   [Flags]
                                   public enum MyFlags
                                   {
                                       None = 0,
                                       A = 1,
                                       B = 2,
                                   }

                                   public class C
                                   {
                                       public bool M(MyFlags flags) => (flags & MyFlags.A) == MyFlags.A;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a negated HasFlag call folds the negation into a <c>!=</c> test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegatedHasFlagIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              [Flags]
                              public enum MyFlags
                              {
                                  None = 0,
                                  A = 1,
                                  B = 2,
                              }

                              public class C
                              {
                                  public bool M(MyFlags flags) => !{|PSH1016:flags.HasFlag(MyFlags.A)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   [Flags]
                                   public enum MyFlags
                                   {
                                       None = 0,
                                       A = 1,
                                       B = 2,
                                   }

                                   public class C
                                   {
                                       public bool M(MyFlags flags) => (flags & MyFlags.A) != MyFlags.A;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a combined flag argument is parenthesized on both sides of the rewrite.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompositeFlagArgumentIsParenthesizedAsync()
    {
        const string Source = """
                              using System;

                              [Flags]
                              public enum MyFlags
                              {
                                  None = 0,
                                  A = 1,
                                  B = 2,
                              }

                              public class C
                              {
                                  public bool M(MyFlags flags) => {|PSH1016:flags.HasFlag(MyFlags.A | MyFlags.B)|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   [Flags]
                                   public enum MyFlags
                                   {
                                       None = 0,
                                       A = 1,
                                       B = 2,
                                   }

                                   public class C
                                   {
                                       public bool M(MyFlags flags) => (flags & (MyFlags.A | MyFlags.B)) == (MyFlags.A | MyFlags.B);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a method-call argument is reported but gets no fix, since the call cannot be repeated safely.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodCallArgumentIsReportedWithoutFixAsync()
    {
        const string Source = """
                              using System;

                              [Flags]
                              public enum MyFlags
                              {
                                  None = 0,
                                  A = 1,
                              }

                              public class C
                              {
                                  public bool M(MyFlags flags) => {|PSH1016:flags.HasFlag(Next())|};

                                  private static MyFlags Next() => MyFlags.A;
                              }
                              """;

        var test = new AnalyzerVerify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = Source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a user-defined HasFlag method on a non-enum type stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UserDefinedHasFlagIsCleanAsync() =>
        VerifyAsync(
            """
            public struct Mask
            {
                public bool HasFlag(Mask other) => true;
            }

            public class C
            {
                public bool M(Mask m, Mask n) => m.HasFlag(n);
            }
            """);

    /// <summary>Verifies HasFlag on a receiver typed as the abstract <c>System.Enum</c> stays clean; the bitwise rewrite could not compile there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EnumTypedReceiverIsCleanAsync() =>
        VerifyAsync(
            """
            using System;

            [Flags]
            public enum MyFlags
            {
                None = 0,
                A = 1,
            }

            public class C
            {
                public bool M(Enum value, MyFlags flag) => value.HasFlag(flag);
            }
            """);

    /// <summary>Verifies a HasFlag call nested inside an argument list composes into the surrounding expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HasFlagInsideArgumentIsFixedAsync()
    {
        const string Source = """
                              using System;

                              [Flags]
                              public enum MyFlags
                              {
                                  None = 0,
                                  A = 1,
                                  B = 2,
                              }

                              public class C
                              {
                                  public string M(MyFlags flags) => Format({|PSH1016:flags.HasFlag(MyFlags.A)|});

                                  private static string Format(bool value) => value.ToString();
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   [Flags]
                                   public enum MyFlags
                                   {
                                       None = 0,
                                       A = 1,
                                       B = 2,
                                   }

                                   public class C
                                   {
                                       public string M(MyFlags flags) => Format((flags & MyFlags.A) == MyFlags.A);

                                       private static string Format(bool value) => value.ToString();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies safe flag operands and surrounding expressions preserve their precedence.</summary>
    /// <param name="expression">The marked expression before rewriting.</param>
    /// <param name="replacement">The expected fixed expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("{|PSH1016:flags.HasFlag(flag)|}", "(flags & flag) == flag")]
    [Arguments("{|PSH1016:flags.HasFlag((flag))|}", "(flags & (flag)) == (flag)")]
    [Arguments("{|PSH1016:flags.HasFlag(this.Flag)|}", "(flags & this.Flag) == this.Flag")]
    [Arguments("{|PSH1016:flags.HasFlag(base.Flag)|}", "(flags & base.Flag) == base.Flag")]
    [Arguments("({|PSH1016:flags.HasFlag(flag)|})", "((flags & flag) == flag)")]
    [Arguments("!({|PSH1016:flags.HasFlag(flag)|})", "!((flags & flag) == flag)")]
    [Arguments("{|PSH1016:flags.HasFlag(flag)|} == true", "((flags & flag) == flag) == true")]
    [Arguments("!{|PSH1016:flags.HasFlag(flag)|} == true", "((flags & flag) != flag) == true")]
    [Arguments("result = {|PSH1016:flags.HasFlag(flag)|}", "result = (flags & flag) == flag")]
    [Arguments("{|PSH1016:flags.HasFlag(flag)|} ? true : false", "((flags & flag) == flag) ? true : false")]
    [Arguments("{|PSH1016:flags.HasFlag(flag)|}.Equals(true)", "((flags & flag) == flag).Equals(true)")]
    [Arguments("{|PSH1016:flags.HasFlag(flag)|} && result", "((flags & flag) == flag) && result")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SafeFlagsPreserveExpressionPrecedenceAsync(string expression, string replacement) =>
        VerifyAsync(
            $$"""
            using System;
            [Flags] public enum MyFlags { A = 1, B = 2 }
            public class B { protected MyFlags Flag; }
            public class C : B
            {
                public bool M(MyFlags flags, MyFlags flag, bool result) => {{expression}};
            }
            """,
            $$"""
            using System;
            [Flags] public enum MyFlags { A = 1, B = 2 }
            public class B { protected MyFlags Flag; }
            public class C : B
            {
                public bool M(MyFlags flags, MyFlags flag, bool result) => {{replacement}};
            }
            """);

    /// <summary>Verifies stale diagnostics and flags with side effects register no action or batch edit.</summary>
    /// <param name="expression">The syntax now occupying the diagnostic location.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("flags")]
    [Arguments("flags.HasFlag()")]
    [Arguments("flags.HasFlag(A, B)")]
    [Arguments("flags.Other(A)")]
    [Arguments("HasFlag(A)")]
    [Arguments("flags.HasFlag(Next())")]
    [Arguments("flags.HasFlag((Next()))")]
    [Arguments("flags.HasFlag(Get().Flag)")]
    [Arguments("flags.HasFlag(A | Next())")]
    [Arguments("flags.HasFlag(Next() | A)")]
    [Arguments("flags.HasFlag(A & B)")]
    [Arguments("flags.HasFlag(values[0])")]
    [Arguments("flags.HasFlag(pointer->Flag)")]
    public async Task StaleOrUnsafeFlagDiagnosticIsIgnoredAsync(string expression)
    {
        var source = $"class C {{ object M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var node = root.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Single().Expression;
        var diagnostic = Diagnostic.Create(AllocationRules.UseBitwiseFlagTest, node.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1016UseBitwiseFlagTestCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var unchanged = Psh1016UseBitwiseFlagTestCodeFixProvider.Apply(document, root, invocation);
        await Assert.That(unchanged).IsSameReferenceAs(document);
    }

    /// <summary>Verifies the syntax helper accepts literal flags and preserves trivia and uncommon parent expressions.</summary>
    /// <param name="expression">The expression containing a HasFlag invocation.</param>
    /// <param name="replacement">The expression after the helper applies the replacement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("flags.HasFlag(0)", "(flags & 0) == 0")]
    [Arguments("flags.HasFlag(this)", "(flags & this) == this")]
    [Arguments("flags.HasFlag(base)", "(flags & base) == base")]
    [Arguments("~flags.HasFlag(A)", "~((flags & A) == A)")]
    [Arguments("flags.HasFlag(A) = value", "((flags & A) == A) = value")]
    [Arguments("/* before */ flags.HasFlag(A) /* after */", "/* before */ (flags & A) == A /* after */")]
    public async Task ApplyHandlesSafeSyntaxAndPreservesTriviaAsync(string expression, string replacement)
    {
        var source = $"class C {{ object M() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(nameof(Test), LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = (await document.GetSyntaxRootAsync())!;
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var changed = Psh1016UseBitwiseFlagTestCodeFixProvider.Apply(document, root, invocation);
        var text = await changed.GetTextAsync();
        await Assert.That(text.ToString()).IsEqualTo($"class C {{ object M() => {replacement}; }}");
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.Net90, TestCode = source, };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
