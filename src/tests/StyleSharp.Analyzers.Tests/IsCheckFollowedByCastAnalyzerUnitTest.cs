// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Composition.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using RoslynCommon.Analyzers.Tests;

using VerifyPatternMatching = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.PatternMatchingAnalyzer,
    StyleSharp.Analyzers.DeclarationPatternCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2007 (is check followed by a cast local).</summary>
public class IsCheckFollowedByCastAnalyzerUnitTest
{
    /// <summary>Verifies stale pattern diagnostics decline individual and batch edits when the cast-local shape has changed.</summary>
    /// <param name="statement">The replacement statement at the diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("return;")]
    [Arguments("if (true) { var text = (string)value; }")]
    [Arguments("if (value is string) return;")]
    [Arguments("if (value is string) { }")]
    [Arguments("if (value is string) { return; }")]
    [Arguments("if (value is string) { string a = (string)value, b = (string)value; }")]
    [Arguments("if (value is string) { var text = value; }")]
    [Arguments("if (value is string) { string text; }")]
    [Arguments("if (value is string) {\n#region Read\nvar text = (string)value;\n#endregion\n}")]
    public async Task ChangedCastLocalHasNoRewriteAsync(string statement)
    {
        var source = $"class C {{ void M(object value) {{ {statement} }} }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var target = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single().Body!.Statements[0];
        var diagnostic = Diagnostic.Create(ModernizationRules.UseDeclarationPatternOverIsCheckAndCast, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<DeclarationPatternCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        await Assert.That(DeclarationPatternCodeFixProvider.Apply(document, root, diagnostic)).IsSameReferenceAs(document);
        var editor = await DocumentEditor.CreateAsync(document);
        ((IBatchFixableCodeFix)provider).RegisterBatchEdits(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies an <c>if</c> carrying a region is reported but not rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The cast local is deleted from the body, and the <c>#region</c> is its leading trivia, so the close
    /// would be left with nothing to close.
    /// </remarks>
    [Test]
    public async Task IfCarryingADirectiveIsNotRewrittenAsync()
    {
        const string Source = """
            public sealed class C
            {
                public int M(object o)
                {
                    if ({|SST2007:o is string|})
                    {
            #region Read
                        var text = (string)o;
                        return text.Length;
            #endregion
                    }

                    return 0;
                }
            }
            """;
        await VerifyPatternMatching.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies an <c>is</c> check followed by a matching local cast is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsCheckFollowedByCastLocalIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(object value)
                                  {
                                      if ({|SST2007:value is string|})
                                      {
                                          var text = (string)value;
                                          return text.Length;
                                      }

                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(object value)
                                       {
                                           if (value is string text)
                                           {
                                               return text.Length;
                                           }

                                           return 0;
                                       }
                                   }
                                   """;
        await VerifyPatternMatching.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies property receivers and mismatched casts are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnsafeOrMismatchedShapesAreCleanAsync() =>
        VerifyPatternMatching.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private readonly object _value = "";

                public object Value { get; } = "";

                public int PropertyReceiver()
                {
                    if (Value is string)
                    {
                        var text = (string)Value;
                        return text.Length;
                    }

                    return 0;
                }

                public int MismatchedCast(object value)
                {
                    if (value is string)
                    {
                        var text = (object)value;
                        return text.GetHashCode();
                    }

                    return 0;
                }

                public int FieldReceiver()
                {
                    if (_value is string)
                    {
                        var text = (string)_value;
                        return text.Length;
                    }

                    return 0;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent below C# 7, where the declaration pattern the fix emits does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp7Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(object value)
                                  {
                                      if (value is string)
                                      {
                                          var text = (string)value;
                                          return text.Length;
                                      }

                                      return 0;
                                  }
                              }
                              """;
        var test = new VerifyPatternMatching.Test { TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp6));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
