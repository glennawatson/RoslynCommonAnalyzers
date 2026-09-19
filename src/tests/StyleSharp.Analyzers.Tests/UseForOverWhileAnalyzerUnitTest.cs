// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using RoslynCommon.Analyzers.Tests;
using VerifyUseForOverWhile = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2287UseForOverWhileAnalyzer,
    StyleSharp.Analyzers.Sst2287UseForOverWhileCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2287UseForOverWhileAnalyzer"/> and its code fix (SST2287).</summary>
public class UseForOverWhileAnalyzerUnitTest
{
    /// <summary>The name used for documents created directly by these tests.</summary>
    private const string TestFileName = "Test.cs";

    /// <summary>The offset used to place a diagnostic beyond the current document.</summary>
    private const int StaleDiagnosticOffset = 10;

    /// <summary>A valid counted while loop used to exercise code-fix registration.</summary>
    private const string ValidCountedWhileSource = "class C { void M(int n) { int i = 0; while (i < n) { System.Console.WriteLine(i); i++; } } }";

    /// <summary>Verifies that a diagnostic cannot offer a rewrite after the body gains a counter assignment.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CounterAssignmentPreventsCodeFixRegistrationAsync()
    {
        const string Source = "class C { void M(int n) { int i = 0; while (i < n) { i = 2; i++; } } }";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(TestFileName, Source);
        var root = (await document.GetSyntaxRootAsync())!;
        var loop = root.DescendantNodes().OfType<WhileStatementSyntax>().Single();
        var descriptor = new Sst2287UseForOverWhileAnalyzer().SupportedDiagnostics[0];
        var diagnostic = Diagnostic.Create(descriptor, loop.WhileKeyword.GetLocation(), "i");
        using var container = new ContainerConfiguration().WithPart<Sst2287UseForOverWhileCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Verifies a cold document loads its root and semantic model when the caches are empty.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ColdDocumentOffersFixAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(TestFileName, ValidCountedWhileSource);
        var start = ValidCountedWhileSource.IndexOf("while", StringComparison.Ordinal);
        var descriptor = new Sst2287UseForOverWhileAnalyzer().SupportedDiagnostics[0];
        var diagnostic = Diagnostic.Create(
            descriptor,
            Location.Create(TestFileName, new(start, "while".Length), default));

        var actions = await RegisterCodeFixesAsync(document, diagnostic);

        await Assert.That(actions).Count().IsEqualTo(1);
    }

    /// <summary>Verifies cached syntax and semantic models register the same fix.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CachedDocumentOffersFixAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(TestFileName, ValidCountedWhileSource);
        var root = (await document.GetSyntaxRootAsync())!;
        _ = await document.GetSemanticModelAsync();
        var loop = root.DescendantNodes().OfType<WhileStatementSyntax>().Single();
        var descriptor = new Sst2287UseForOverWhileAnalyzer().SupportedDiagnostics[0];
        var diagnostic = Diagnostic.Create(descriptor, loop.WhileKeyword.GetLocation());

        var actions = await RegisterCodeFixesAsync(document, diagnostic);

        await Assert.That(actions).Count().IsEqualTo(1);
    }

    /// <summary>Verifies a stale diagnostic span is ignored without loading a semantic model.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StaleDiagnosticSpanIsIgnoredAsync()
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp)
            .WithMetadataReferences(RuntimeMetadataReferences.Platform)
            .AddDocument(TestFileName, ValidCountedWhileSource);
        var descriptor = new Sst2287UseForOverWhileAnalyzer().SupportedDiagnostics[0];
        var diagnostic = Diagnostic.Create(
            descriptor,
            Location.Create(TestFileName, new(ValidCountedWhileSource.Length + StaleDiagnosticOffset, 0), default));

        var actions = await RegisterCodeFixesAsync(document, diagnostic);

        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Verifies that additional counter writes keep a self-advancing loop in while form.</summary>
    /// <param name="statement">The body statement that can write the counter.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("i = i > 0 ? i : 1;")]
    [Arguments("i += 2;")]
    [Arguments("i -= 2;")]
    [Arguments("++i;")]
    [Arguments("i++;")]
    [Arguments("--i;")]
    [Arguments("i--;")]
    [Arguments("{ i = 2; }")]
    [Arguments("(i) = 2;")]
    [Arguments("(i, n) = (2, 10);")]
    [Arguments("Assign(out i);")]
    [Arguments("Advance(ref i);")]
    [Arguments("ref int alias = ref i; alias++;")]
    [Arguments("if (i == 1) { i = 2; }")]
    [Arguments("System.Action advance = () => i++; advance();")]
    public async Task AdditionalCounterWriteIsCleanAsync(string statement)
    {
        var source = $$"""
                       internal class C
                       {
                           public void M(int n)
                           {
                               int i = 0;
                               while (i < n)
                               {
                                   {{statement}}
                                   i++;
                               }
                           }

                           private static void Assign(out int value) => value = 2;
                           private static void Advance(ref int value) => value++;
                       }
                       """;
        await VerifyUseForOverWhile.VerifyAnalyzerAsync(source);
    }

    /// <summary>Verifies that reads and writes to other symbols do not prevent gathering the counter.</summary>
    /// <param name="statement">A statement that does not write the counter.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("this.i++;")]
    [Arguments("System.Action<int> advance = i => i++; advance(0);")]
    [Arguments("Read(in i);")]
    public async Task BodyWithoutCounterWriteIsFlaggedAndFixedAsync(string statement)
    {
        var source = $$"""
                       internal class C
                       {
                           private int i;

                           public void M(int n)
                           {
                               int i = 0;
                               {|SST2287:while (i < n)|}
                               {
                                   {{statement}}
                                   i++;
                               }
                           }

                           private static void Read(in int value) { }
                       }
                       """;
        var fixedSource = $$"""
                            internal class C
                            {
                                private int i;

                                public void M(int n)
                                {
                                    for (int i = 0; i < n; i++)
                                    {
                                        {{statement}}
                                    }
                                }

                                private static void Read(in int value) { }
                            }
                            """;
        await VerifyUseForOverWhile.VerifyCodeFixAsync(source, fixedSource);
    }

    /// <summary>Verifies a counter-owning while loop is reported and gathered into a for header.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CounterOwningWhileIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M(int n)
                                  {
                                      int i = 0;
                                      {|SST2287:while (i < n)|}
                                      {
                                          System.Console.WriteLine(i);
                                          i++;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M(int n)
                                       {
                                           for (int i = 0; i < n; i++)
                                           {
                                               System.Console.WriteLine(i);
                                           }
                                       }
                                   }
                                   """;
        await VerifyUseForOverWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a compound-assignment step is gathered into the header.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompoundAssignmentStepIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M(int n)
                                  {
                                      int i = 0;
                                      {|SST2287:while (i < n)|}
                                      {
                                          System.Console.WriteLine(i);
                                          i += 2;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M(int n)
                                       {
                                           for (int i = 0; i < n; i += 2)
                                           {
                                               System.Console.WriteLine(i);
                                           }
                                       }
                                   }
                                   """;
        await VerifyUseForOverWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a downward-counting loop with a prefix step is gathered into the header.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrefixDecrementStepIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M(int n)
                                  {
                                      int i = n;
                                      {|SST2287:while (i > 0)|}
                                      {
                                          System.Console.WriteLine(i);
                                          --i;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M(int n)
                                       {
                                           for (int i = n; i > 0; --i)
                                           {
                                               System.Console.WriteLine(i);
                                           }
                                       }
                                   }
                                   """;
        await VerifyUseForOverWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies statements around the loop are left where they are.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SurroundingStatementsAreKeptAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public int M(int n)
                                  {
                                      int total = 0;
                                      int i = 0;
                                      {|SST2287:while (i < n)|}
                                      {
                                          total += i;
                                          i++;
                                      }

                                      return total;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int M(int n)
                                       {
                                           int total = 0;
                                           for (int i = 0; i < n; i++)
                                           {
                                               total += i;
                                           }

                                           return total;
                                       }
                                   }
                                   """;
        await VerifyUseForOverWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a counter read after the loop keeps the while form; a for header would scope it away.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CounterReadAfterLoopIsCleanAsync() =>
        VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(int n)
                {
                    int i = 0;
                    while (i < n)
                    {
                        System.Console.WriteLine(i);
                        i++;
                    }

                    return i;
                }
            }
            """);

    /// <summary>Verifies a body holding a continue is left alone; a for header would run the step it skips.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BodyWithContinueIsCleanAsync() =>
        VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i = 0;
                    while (i < n)
                    {
                        if (i == 2)
                        {
                            i = 5;
                            continue;
                        }

                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies a continue belonging to a nested loop does not block the rewrite.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContinueInNestedLoopIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public void M(int n)
                                  {
                                      int i = 0;
                                      {|SST2287:while (i < n)|}
                                      {
                                          foreach (var value in new int[0])
                                          {
                                              if (value == 0)
                                              {
                                                  continue;
                                              }
                                          }

                                          i++;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public void M(int n)
                                       {
                                           for (int i = 0; i < n; i++)
                                           {
                                               foreach (var value in new int[0])
                                               {
                                                   if (value == 0)
                                                   {
                                                       continue;
                                                   }
                                               }
                                           }
                                       }
                                   }
                                   """;
        await VerifyUseForOverWhile.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a loop whose condition ignores the declared local is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConditionWithoutCounterIsCleanAsync() =>
        VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(bool flag)
                {
                    int i = 0;
                    while (flag)
                    {
                        System.Console.WriteLine(i);
                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies a loop that does not end by stepping the counter is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoopWithoutTrailingStepIsCleanAsync() =>
        VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i = 0;
                    while (i < n)
                    {
                        i++;
                        System.Console.WriteLine(i);
                    }
                }
            }
            """);

    /// <summary>Verifies a loop with no declaration above it is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LoopWithoutPrecedingDeclarationIsCleanAsync() =>
        VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n, int i)
                {
                    System.Console.WriteLine(n);
                    while (i < n)
                    {
                        System.Console.WriteLine(i);
                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies a declaration of two variables is left alone; a for header declares one group.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MultipleDeclaratorsAreCleanAsync() =>
        VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i = 0, j = 1;
                    while (i < n)
                    {
                        System.Console.WriteLine(j);
                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies an uninitialized declaration is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UninitializedDeclarationIsCleanAsync() =>
        VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i;
                    i = 0;
                    while (i < n)
                    {
                        System.Console.WriteLine(i);
                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies a single-statement body is left alone; there is no work left once the step moves out.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleStatementBodyIsCleanAsync() =>
        VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i = 0;
                    while (i < n)
                    {
                        i++;
                    }
                }
            }
            """);

    /// <summary>Verifies a brace-less loop body is left alone; there is no trailing statement to lift.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmbeddedLoopBodyIsCleanAsync() =>
        VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i = 0;
                    while (i < n)
                        i++;
                }
            }
            """);

    /// <summary>Verifies a step whose amount reads the counter itself is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SelfReferencingStepIsCleanAsync() =>
        VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public void M(int n)
                {
                    int i = 1;
                    while (i < n)
                    {
                        System.Console.WriteLine(i);
                        i += i;
                    }
                }
            }
            """);

    /// <summary>Verifies a counter written after the loop keeps the while form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CounterWrittenAfterLoopIsCleanAsync() =>
        VerifyUseForOverWhile.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(int n)
                {
                    int i = 0;
                    while (i < n)
                    {
                        System.Console.WriteLine(i);
                        i++;
                    }

                    i = 0;
                    return n;
                }
            }
            """);

    /// <summary>Registers one code-fix diagnostic with the provider under test.</summary>
    /// <param name="document">The document containing the diagnostic.</param>
    /// <param name="diagnostic">The diagnostic to register.</param>
    /// <returns>The code actions registered by the provider.</returns>
    private static async Task<List<CodeAction>> RegisterCodeFixesAsync(Document document, Diagnostic diagnostic)
    {
        using var container = new ContainerConfiguration().WithPart<Sst2287UseForOverWhileCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        return actions;
    }
}
