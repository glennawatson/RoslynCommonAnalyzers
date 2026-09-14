// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1023PreferTupleOverAnonymousTypeAnalyzer,
    PerformanceSharp.Analyzers.Psh1023PreferTupleOverAnonymousTypeCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1023 (a local anonymous type that could be a tuple).</summary>
public class PreferTupleOverAnonymousTypeAnalyzerUnitTest
{
    /// <summary>Verifies identifier-inferred names survive conversion to tuple elements.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task IdentifierInferredMembersBecomeNamedTupleElementsAsync() =>
        Verify.VerifyCodeFixAsync(
            "class C { int M(int left, int right) { var pair = {|PSH1023:new { left, right }|}; return pair.left + pair.right; } }",
            "class C { int M(int left, int right) { var pair = (left: left, right: right); return pair.left + pair.right; } }");

    /// <summary>Verifies stale targets and anonymous members without inferable names offer no edit.</summary>
    /// <param name="expression">The stale or incomplete expression.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("42")]
    [Arguments("new { 1, Other = 2 }")]
    public async Task InapplicableTupleTargetIsUnchangedAsync(string expression)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("TupleTarget", LanguageNames.CSharp)
            .AddDocument("Test.cs", $"class C {{ void M() {{ var value = {expression}; }} }}");
        var root = (await document.GetSyntaxRootAsync())!;
        var initializer = root.DescendantNodes().OfType<EqualsValueClauseSyntax>().Single().Value;
        var diagnostic = Diagnostic.Create(AllocationRules.PreferTupleOverAnonymousType, initializer.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Psh1023PreferTupleOverAnonymousTypeCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(root.ToFullString());
    }

    /// <summary>Verifies a member-read-only local is reported and rewritten as a tuple.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalReadThroughMembersRewrittenAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M()
                                  {
                                      var pair = {|PSH1023:new { Left = 1, Right = 2 }|};
                                      return pair.Left + pair.Right;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M()
                                       {
                                           var pair = (Left: 1, Right: 2);
                                           return pair.Left + pair.Right;
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an unnamed member takes the name its expression implies.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InferredMemberNamesRewrittenAsync()
    {
        const string Source = """
                              public sealed class Source
                              {
                                  public int Width { get; set; }

                                  public int Height { get; set; }
                              }

                              public sealed class C
                              {
                                  public int M(Source source)
                                  {
                                      var size = {|PSH1023:new { source.Width, source.Height }|};
                                      return size.Width * size.Height;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class Source
                                   {
                                       public int Width { get; set; }

                                       public int Height { get; set; }
                                   }

                                   public sealed class C
                                   {
                                       public int M(Source source)
                                       {
                                           var size = (Width: source.Width, Height: source.Height);
                                           return size.Width * size.Height;
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a local that is returned is left alone, since its type is part of a contract.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReturnedLocalIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public object M()
                {
                    var pair = new { Left = 1, Right = 2 };
                    return pair;
                }
            }
            """);

    /// <summary>Verifies a local passed on as an argument is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PassedLocalIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Consume(object value)
                {
                }

                public void M()
                {
                    var pair = new { Left = 1, Right = 2 };
                    Consume(pair);
                }
            }
            """);

    /// <summary>Verifies a single-member anonymous type is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleMemberIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M()
                {
                    var wrapper = new { Only = 1 };
                    return wrapper.Only;
                }
            }
            """);

    /// <summary>Verifies an anonymous type that is not a local's initializer is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonLocalAnonymousTypeIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Consume(object value)
                {
                }

                public void M() => Consume(new { Left = 1, Right = 2 });
            }
            """);

    /// <summary>Verifies the rule fires on C# 7, the version that introduced named tuples.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReportedOnTheIntroducingVersionAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M()
                                  {
                                      var pair = {|PSH1023:new { Left = 1, Right = 2 }|};
                                      return pair.Left + pair.Right;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M()
                                       {
                                           var pair = (Left: 1, Right: 2);
                                           return pair.Left + pair.Right;
                                       }
                                   }
                                   """;

        var test = new Verify.Test { TestCode = Source, FixedCode = FixedSource, };

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp7));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
