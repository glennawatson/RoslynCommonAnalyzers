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
using RoslynCommon.Analyzers.Tests;
using VerifyNamedArgumentOrder = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1220NamedArgumentOrderAnalyzer,
    StyleSharp.Analyzers.Sst1220NamedArgumentOrderCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the named-argument order rule (SST1220) and its reorder fix.</summary>
public class NamedArgumentOrderAnalyzerUnitTest
{
    /// <summary>Verifies stale diagnostics cannot reorder missing, partial, or unresolved argument lists.</summary>
    /// <param name="expression">The expression at the diagnostic.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("42")]
    [Arguments("M(a: 1)")]
    [Arguments("Missing(a: 1, b: 2)")]
    [Arguments("M(1, b: 2)")]
    [Arguments("M(a: 1, missing: 2)")]
    public async Task UnsortableArgumentsHaveNoRewriteAsync(string expression)
    {
        var source = $"class C {{ int M(int a, int b = 0) => a + b; int Use() => {expression}; }}";
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).WithMetadataReferences(RuntimeMetadataReferences.Platform).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var expressionNode = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Last().ExpressionBody!.Expression;
        SyntaxNode target = expressionNode is InvocationExpressionSyntax invocation ? invocation.ArgumentList : expressionNode;
        var diagnostic = Diagnostic.Create(OrderingRules.NamedArgumentOrder, target.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst1220NamedArgumentOrderCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
        var editor = await DocumentEditor.CreateAsync(document);
        BatchEditRegistration.Register<Sst1220NamedArgumentOrderCodeFixProvider>(editor, diagnostic);
        await Assert.That(editor.GetChangedRoot().ToFullString()).IsEqualTo(source);
    }

    /// <summary>Verifies an out-of-order all-named call is reported and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReorderedInvocationReportedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private static void M(int a, int b)
                                  {
                                  }

                                  private static void Caller() => M{|SST1220:(b: 1, a: 2)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private static void M(int a, int b)
                                       {
                                       }

                                       private static void Caller() => M(a: 2, b: 1);
                                   }
                                   """;
        await VerifyNamedArgumentOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an out-of-order all-named object creation is reported and reordered.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReorderedObjectCreationReportedAsync()
    {
        const string Source = """
                              public class Point
                              {
                                  public Point(int x, int y)
                                  {
                                  }

                                  public static Point Make() => new Point{|SST1220:(y: 2, x: 1)|};
                              }
                              """;
        const string FixedSource = """
                                   public class Point
                                   {
                                       public Point(int x, int y)
                                       {
                                       }

                                       public static Point Make() => new Point(x: 1, y: 2);
                                   }
                                   """;
        await VerifyNamedArgumentOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies all-named arguments already in declaration order are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InOrderArgumentsAreCleanAsync() =>
        VerifyNamedArgumentOrder.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static void M(int a, int b)
                {
                }

                private static void Caller() => M(a: 1, b: 2);
            }
            """);

    /// <summary>Verifies a call that is not fully named is not reported, even when out of order.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PartiallyNamedCallIsCleanAsync() =>
        VerifyNamedArgumentOrder.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static void M(int a, int b)
                {
                }

                private static void Caller() => M(1, b: 2);
            }
            """);
}
