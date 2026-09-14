// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyModernSyntaxFlow = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ModernSyntaxFlowAnalyzer,
    StyleSharp.Analyzers.ModernSyntaxFlowCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for flow-shaped modern syntax rules (SST2207/SST2208).</summary>
public class ModernSyntaxFlowAnalyzerUnitTest
{
    /// <summary>Verifies applying stale flow diagnostics preserves statements that no longer form a supported pair.</summary>
    /// <param name="id">The diagnostic being applied.</param>
    /// <param name="body">The method body at application time.</param>
    /// <param name="targetClass">Whether the diagnostic has moved outside all statements.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("SST2207", "if (value == null) return value; return value;", false)]
    [Arguments("SST2207", "return value;", false)]
    [Arguments("SST2208", "int number;", false)]
    [Arguments("SST2208", "int number = 1; return value;", false)]
    [Arguments("SST2208", "return value;", false)]
    [Arguments("SST2207", "return value;", true)]
    [Arguments("SST2208", "return value;", true)]
    [Arguments("SST9999", "return value;", true)]
    public async Task StaleFlowDiagnosticLeavesDocumentUnchangedAsync(string id, string body, bool targetClass)
    {
        var source = $"class C {{ string M(string value) {{ {body} }} }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        SyntaxNode target = targetClass ? root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single() : root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single().Body!.Statements[0];
        var descriptor = new DiagnosticDescriptor(id, "Flow", "Flow", "Style", DiagnosticSeverity.Info, true);
        var diagnostic = Diagnostic.Create(descriptor, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<ModernSyntaxFlowCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(id == "SST9999" ? 0 : 1);
        await Assert.That(await ModernSyntaxFlowCodeFixProvider.ApplyAsync(document, root, diagnostic, CancellationToken.None)).IsSameReferenceAs(document);
    }

    /// <summary>Verifies equality guards support either null operand and redundant parentheses.</summary>
    /// <param name="condition">The supported null check.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value == null")]
    [Arguments("null == value")]
    [Arguments("((value) == (null))")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EqualityNullGuardIsReportedAsync(string condition) =>
        VerifyModernSyntaxFlow.VerifyAnalyzerAsync($$"""
            class C
            {
                string M(string value)
                {
                    {|SST2207:if|} ({{condition}}) throw new System.Exception();
                    return value;
                }
            }
            """);

    /// <summary>Verifies near-miss guards cannot be folded into a return expression.</summary>
    /// <param name="body">The guard and following statements.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("if (value == null) throw new System.Exception(); else { } return value;")]
    [Arguments("if (value != null) throw new System.Exception(); return value;")]
    [Arguments("if (value == other) throw new System.Exception(); return value;")]
    [Arguments("if (null == null) throw new System.Exception(); return value;")]
    [Arguments("if (value is string) throw new System.Exception(); return value;")]
    [Arguments("if (value == null) { } return value;")]
    [Arguments("if (value == null) { value = other; throw new System.Exception(); } return value;")]
    [Arguments("if (value == null) value = other; return value;")]
    [Arguments("if (value == null) throw new System.Exception(); return other;")]
    [Arguments("if (value == null) throw new System.Exception(); return value + other;")]
    [Arguments("\n\n    // guard\nif (value == null) throw new System.Exception(); return value;")]
    [Arguments("if (value == null) throw new System.Exception(); // guard\nreturn value;")]
    [Arguments("try { return value; } catch { if (value == null) throw; return value; }")]
    [Arguments("while (value == null) if (value == null) throw new System.Exception(); return value;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnsupportedGuardIsCleanAsync(string body) =>
        VerifyModernSyntaxFlow.VerifyAnalyzerAsync($"class C {{ string M(string value, string other) {{ {body} }} }}");

    /// <summary>Verifies declarations with no unique immediate out argument remain unchanged.</summary>
    /// <param name="body">The declarations and their first use.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int value, other; int.TryParse(text, out value); int.TryParse(text, out other);")]
    [Arguments("var value = 0; int.TryParse(text, out value);")]
    [Arguments("int value = 0; int.TryParse(text, out value);")]
    [Arguments("int value;")]
    [Arguments("int value; System.Console.WriteLine(text);")]
    [Arguments("int value; int.TryParse(text, out var other);")]
    [Arguments("int value; Both(out value, out value);")]
    [Arguments("int value; System.Action action = () => int.TryParse(text, out value);")]
    [Arguments("\n// value\nint value; int.TryParse(text, out value);")]
    [Arguments("int value; // value\nint.TryParse(text, out value);")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnsupportedOutDeclarationIsCleanAsync(string body) =>
        VerifyModernSyntaxFlow.VerifyAnalyzerAsync($$"""
            class C
            {
                void M(string text) { {{body}} }
                void Both(out int first, out int second) { first = second = 0; }
            }
            """);

    /// <summary>Verifies declarations inline when every reference remains within the new scope.</summary>
    /// <param name="statement">The first use and any later references.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int.TryParse(text, out value); System.Console.WriteLine(value);")]
    [Arguments("{ int.TryParse(text, out value); System.Console.WriteLine(value); } System.Console.WriteLine(text);")]
    [Arguments("while (int.TryParse(text, out value)) { System.Console.WriteLine(value); }")]
    [Arguments("do { } while (int.TryParse(text, out value));")]
    [Arguments("for (; int.TryParse(text, out value);) { System.Console.WriteLine(value); }")]
    [Arguments("for (int i = int.TryParse(text, out value) ? 0 : 1;;) { break; }")]
    [Arguments("switch (int.TryParse(text, out value)) { default: System.Console.WriteLine(value); break; }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task OutVariableReferencesRemainInScopeAsync(string statement) =>
        VerifyModernSyntaxFlow.VerifyAnalyzerAsync($"class C {{ void M(string text) {{ int {{|SST2208:value|}}; {statement} }} }}");

    /// <summary>Verifies loop and switch condition declarations cannot hide a later use.</summary>
    /// <param name="statement">The restricted-scope statement.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("do { } while (int.TryParse(text, out value));")]
    [Arguments("for (; int.TryParse(text, out value);) { }")]
    [Arguments("switch (int.TryParse(text, out value)) { default: break; }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task OutVariableUseAfterRestrictedScopeIsCleanAsync(string statement) =>
        VerifyModernSyntaxFlow.VerifyAnalyzerAsync($"class C {{ void M(string text) {{ int value; {statement} System.Console.WriteLine(value); }} }}");

    /// <summary>Verifies unresolved symbols are rejected before a throw expression is suggested.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnresolvedGuardDoesNotMatchAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { object M() { if (missing == null) throw new System.Exception(); return missing; } }");
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform);
        var statement = (await tree.GetRootAsync()).DescendantNodes().OfType<IfStatementSyntax>().Single();
        var matched = ModernSyntaxFlowAnalyzer.TryGetThrowExpressionCandidate(statement, compilation.GetSemanticModel(tree), CancellationToken.None, out var expression);
        await Assert.That(matched).IsFalse();
        await Assert.That(expression).IsNull();
    }

    /// <summary>Verifies a final statement and a detached statement have no following sibling.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StatementWithoutFollowingSiblingIsRejectedAsync()
    {
        var block = (BlockSyntax)SyntaxFactory.ParseStatement("{ return; }");
        await Assert.That(ModernSyntaxFlowAnalyzer.TryGetNextStatement(block.Statements[0], out _)).IsFalse();
        await Assert.That(ModernSyntaxFlowAnalyzer.TryGetNextStatement(SyntaxFactory.ReturnStatement(), out _)).IsFalse();
    }

    /// <summary>Verifies a same-named field after an inner scope does not keep the local declaration outside it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SameNamedFieldDoesNotExtendLocalScopeAsync() =>
        VerifyModernSyntaxFlow.VerifyAnalyzerAsync("""
            class C
            {
                int value;
                void M(string text)
                {
                    int {|SST2208:value|};
                    while (int.TryParse(text, out value)) { }
                    System.Console.WriteLine(this.value);
                }
            }
            """);

    /// <summary>Verifies a same-named field before a nested condition does not count as an earlier local use.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SameNamedFieldBeforeInlineScopeIsIgnoredAsync() =>
        VerifyModernSyntaxFlow.VerifyAnalyzerAsync("""
            class C
            {
                int value;
                void M(string text)
                {
                    int {|SST2208:value|};
                    {
                        System.Console.WriteLine(this.value);
                        if (int.TryParse(text, out value)) { }
                    }
                }
            }
            """);

    /// <summary>Verifies a nested local function's same-named parameter is not the outer local.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SameNamedParameterIsNotAnOutUseOfTheLocalAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { void M() { int value; void Local(int value) { int.TryParse(string.Empty, out value); } } }");
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform);
        var root = await tree.GetRootAsync();
        var declaration = root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Single();
        var next = root.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Single();
        var matches = ModernSyntaxFlowAnalyzer.TryGetInlineOutArgument(declaration, next, compilation.GetSemanticModel(tree), CancellationToken.None, out var argument);
        await Assert.That(matches).IsFalse();
        await Assert.That(argument).IsNull();
    }

    /// <summary>Verifies a guard separated from its return by a region is reported but not folded.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Folding swallows the following return, and the <c>#region</c> is its leading trivia, so the close
    /// would be left with nothing to close.
    /// </remarks>
    [Test]
    public async Task GuardAcrossADirectiveIsNotFoldedAsync()
    {
        const string Source = """
            using System;

            public sealed class C
            {
                public string M(string value)
                {
                    {|SST2207:if|} (value is null)
                    {
                        throw new ArgumentNullException(nameof(value));
                    }
            #region Result
                    return value;
            #endregion
                }
            }
            """;
        await VerifyModernSyntaxFlow.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a null guard plus return can use a throw expression.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullGuardReturnCandidateIsFixedAsync()
    {
        const string Source = """
                              #nullable enable

                              using System;

                              public sealed class C
                              {
                                  public string M(string? value)
                                  {
                                      {|SST2207:if|} (value is null)
                                      {
                                          throw new ArgumentNullException(nameof(value));
                                      }

                                      return value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable

                                   using System;

                                   public sealed class C
                                   {
                                       public string M(string? value)
                                       {
                                           return value ?? throw new ArgumentNullException(nameof(value));
                                       }
                                   }
                                   """;
        var test = new VerifyModernSyntaxFlow.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = FixedSource };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an out local declared for the next statement is moved to the out argument.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OutVariableDeclarationCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(string text)
                                  {
                                      int {|SST2208:value|};
                                      if (int.TryParse(text, out value))
                                      {
                                          return value;
                                      }

                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(string text)
                                       {
                                           if (int.TryParse(text, out var value))
                                           {
                                               return value;
                                           }

                                           return 0;
                                       }
                                   }
                                   """;
        var test = new VerifyModernSyntaxFlow.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = FixedSource };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies guarded shapes with extra work stay clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonCandidatesAreCleanAsync()
    {
        const string Source = """
                              #nullable enable

                              using System;

                              public sealed class C
                              {
                                  public string M(string? value)
                                  {
                                      if (value is null)
                                      {
                                          throw new ArgumentNullException(nameof(value));
                                      }

                                      return value.ToString();
                                  }

                                  public int Parse(string text)
                                  {
                                      int value = 0;
                                      if (int.TryParse(text, out value))
                                      {
                                          return value;
                                      }

                                      return value;
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxFlow.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an out local is not inlined into a nested block when later code still needs the outer scope.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NestedOutVariableUsedAfterNextStatementIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly object gate = new();

                                  private void RunDue()
                                  {
                                      while (true)
                                      {
                                          TimedWorkItem next;
                                          lock (gate)
                                          {
                                              if (!TryDequeueDueNoLock(out next))
                                              {
                                                  ArmTimerNoLock();
                                                  return;
                                              }
                                          }

                                          ExecuteQueued(next.Item);
                                      }
                                  }

                                  private static void ArmTimerNoLock()
                                  {
                                  }

                                  private static void ExecuteQueued(object item)
                                  {
                                  }

                                  private static bool TryDequeueDueNoLock(out TimedWorkItem item)
                                  {
                                      item = new TimedWorkItem();
                                      return true;
                                  }

                                  private readonly struct TimedWorkItem
                                  {
                                      public object Item => new();
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxFlow.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an out local is not inlined into a loop condition when later code still needs the outer scope.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LoopConditionOutVariableUsedAfterLoopIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public void M(Dictionary<string, int> counts, string keyName)
                                  {
                                      int value;
                                      while (counts.TryGetValue(keyName, out value))
                                      {
                                          keyName = $"{keyName}{++value}";
                                      }

                                      counts[keyName] = value;
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxFlow.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the inline out-variable rule stays silent below C# 7, where inline out declarations do not exist.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InlineOutVariableIsSilentBelowCSharp7Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(string text)
                                  {
                                      int value;
                                      if (int.TryParse(text, out value))
                                      {
                                          return value;
                                      }

                                      return 0;
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxFlow.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp6));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
