// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyStaticFunction = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1000StaticAnonymousFunctionAnalyzer,
    PerformanceSharp.Analyzers.Psh1000StaticAnonymousFunctionCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <content>Checks local-function references across anonymous-function boundaries.</content>
public partial class StaticAnonymousFunctionAnalyzerUnitTest
{
    /// <summary>A non-static local function outside the lambda prevents adding static, even without captured variables.</summary>
    /// <param name="localBody">The local function's implementation.</param>
    /// <param name="function">The anonymous function referencing the local function.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    public async Task ExternalNonStaticLocalFunctionIsCleanAsync(
        [Matrix("value", "1")] string localBody,
        [Matrix(
            "() => Local()",
            "() => false ? Local() : 1",
            "delegate { return Local(); }",
            "() => { System.Func<int> handler = Local; return handler(); }",
            "() => { int Nested() => Local(); return Nested(); }",
            "() => { System.Func<int> nested = () => Local(); return nested(); }")] string function,
        CancellationToken cancellationToken)
    {
        var source = $$"""
                       public class C
                       {
                           public System.Func<int> M(int value)
                           {
                               int Local() => {{localBody}};
                               return {{function}};
                           }
                       }
                       """;
        var test = new VerifyStaticFunction.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source };
        await test.RunAsync(cancellationToken);
    }

    /// <summary>Enclosing local functions remain non-static dependencies across declaration order and nested scopes.</summary>
    /// <param name="member">The member containing the anonymous function.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public System.Func<int> M(int value) { System.Func<int> result = () => Local(); int Local() => value; return result; }")]
    [Arguments("public System.Func<int> M(int value) { int Local() => value; { return () => Local(); } }")]
    [Arguments("public System.Func<int> M(int value) { int Local() => value; System.Func<int> Outer() => () => Local(); return Outer(); }")]
    [Arguments("public System.Func<int> Value { get { int Local() => 1; return () => Local(); } }")]
    [Arguments("public System.Func<int> M(int value) { switch (value) { default: int Local() => value; return () => Local(); } }")]
    public async Task EnclosingLocalFunctionScopesStayNonStaticAsync(string member, CancellationToken cancellationToken)
    {
        var test = new VerifyStaticFunction.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = $$"""
                public class C
                {
                    {{member}}
                }
                """,
        };
        await test.RunAsync(cancellationToken);
    }

    /// <summary>An async event callback cannot become static while calling an enclosing non-static local function.</summary>
    /// <param name="localBody">The expression returned by the local function.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value")]
    [Arguments("1")]
    public async Task EventCallbackCallingNonStaticLocalFunctionIsCleanAsync(string localBody, CancellationToken cancellationToken)
    {
        var source = $$"""
                       public class C
                       {
                           public System.EventHandler M(int value)
                           {
                               System.Threading.Tasks.Task<int> Local() => System.Threading.Tasks.Task.FromResult({{localBody}});
                               return async (sender, e) => await Local();
                           }
                       }
                       """;
        var test = new VerifyStaticFunction.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source };
        await test.RunAsync(cancellationToken);
    }

    /// <summary>The compiler rejects references to enclosing non-static local functions from static lambdas.</summary>
    /// <param name="localBody">The local function's implementation.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value")]
    [Arguments("1")]
    public async Task StaticLambdaReferencingNonStaticLocalFunctionDoesNotCompileAsync(string localBody, CancellationToken cancellationToken)
    {
        var source = $$"""
                       public class C
                       {
                           public System.Func<int> M(int value)
                           {
                               int Local() => {{localBody}};
                               return static () => Local();
                           }
                       }
                       """;
        var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
        var compilation = CSharpCompilation.Create(
            "StaticLambdaLocalFunction",
            [tree],
            RuntimeMetadataReferences.Platform,
            new(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics(cancellationToken).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        await Assert.That(errors.Select(static diagnostic => diagnostic.Id)).IsEquivalentTo(["CS8820"]);
    }

    /// <summary>Static local functions, names, and local functions declared inside the lambda permit a compiling static fix.</summary>
    /// <param name="declaration">An enclosing local-function declaration.</param>
    /// <param name="body">The lambda body.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("static int Local() => 1;", "Local()")]
    [Arguments("static int Local<T>() => 1;", "Local<int>()")]
    [Arguments("static int Local() => 1;", "{ System.Func<int> handler = Local; return handler(); }")]
    [Arguments("int Local() => 1;", "nameof(Local).Length")]
    [Arguments("int Local() => 1; System.Func<int> sibling = () => Local();", "1")]
    [Arguments("int Local() => 1; System.Func<int> sibling = () => Local();", "nameof(Local).Length")]
    [Arguments("int Local() => 1; System.Func<int> sibling = () => Local();", "{ int Local() => 2; return Local(); }")]
    [Arguments("", "{ int Local() => 1; return Local(); }")]
    [Arguments("", "{ int Outer() { int Local() => 1; return Local(); } return Outer(); }")]
    [Arguments("", "{ int Local() => 1; System.Func<int> handler = Local; return handler(); }")]
    [Arguments("", "{ int Local() => 1; System.Func<int> nested = () => Local(); return nested(); }")]
    public async Task AccessibleLocalFunctionReferenceMadeStaticAsync(string declaration, string body, CancellationToken cancellationToken)
    {
        var source = CreateSource("{|PSH1000:()|}");
        var fixedSource = CreateSource("static ()");
        var test = new VerifyStaticFunction.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };
        await test.RunAsync(cancellationToken);

        string CreateSource(string parameters) => $$"""
                                                   public class C
                                                   {
                                                       public System.Func<int> M()
                                                       {
                                                           {{declaration}}
                                                           return {{parameters}} => {{body}};
                                                       }
                                                   }
                                                   """;
    }

    /// <summary>A same-named static member remains accessible when a sibling lambda invokes an enclosing local function.</summary>
    /// <param name="member">The external static member declaration.</param>
    /// <param name="body">The lambda body referencing that member.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public static int Local() => 1;", "External.Local()")]
    [Arguments("public static int Local { get; } = 1;", "External.Local")]
    public async Task SameNamedExternalMemberMadeStaticAsync(string member, string body, CancellationToken cancellationToken)
    {
        var source = CreateSource("{|PSH1000:()|}");
        var fixedSource = CreateSource("static ()");
        var test = new VerifyStaticFunction.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };
        await test.RunAsync(cancellationToken);

        string CreateSource(string parameters) => $$"""
                                                   public class C
                                                   {
                                                       public System.Func<int> M(int value)
                                                       {
                                                           int Local() => value;
                                                           System.Func<int> sibling = () => Local();
                                                           return {{parameters}} => {{body}};
                                                       }
                                                   }

                                                   public static class External
                                                   {
                                                       {{member}}
                                                   }
                                                   """;
    }

    /// <summary>Generic local-function calls prevent static simple lambdas while static declarations permit the fix.</summary>
    /// <param name="modifier">The local-function modifier.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("static ")]
    public async Task GenericLocalFunctionInSimpleLambdaRespectsStaticAsync(string modifier, CancellationToken cancellationToken)
    {
        var reports = modifier.Length != 0;
        var source = CreateSource(reports ? "{|PSH1000:value|}" : "value");
        var fixedSource = CreateSource(reports ? "static value" : "value");
        var test = new VerifyStaticFunction.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };
        await test.RunAsync(cancellationToken);

        string CreateSource(string parameter) => $$"""
                                                  public class C
                                                  {
                                                      public System.Func<int, int> M()
                                                      {
                                                          {{modifier}}T Local<T>(T value) => value;
                                                          return {{parameter}} => Local<int>(value);
                                                      }
                                                  }
                                                  """;
    }

    /// <summary>Fix All preserves local functions declared in nested lambdas while making both lambdas static.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NestedLambdasWithInternalLocalFunctionMadeStaticAsync(CancellationToken cancellationToken)
    {
        const string Source = """
                              public class C
                              {
                                  public System.Func<System.Func<int>> M() => {|PSH1000:()|} => {|PSH1000:()|} => { int Local() => 1; return Local(); };
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.Func<System.Func<int>> M() => static () => static () => { int Local() => 1; return Local(); };
                                   }
                                   """;
        var test = new VerifyStaticFunction.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = Source, FixedCode = FixedSource };
        await test.RunAsync(cancellationToken);
    }

    /// <summary>A static local function permits a static async event callback without changing its void contract.</summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EventCallbackCallingStaticLocalFunctionMadeStaticAsync(CancellationToken cancellationToken)
    {
        const string Source = """
                              public class C
                              {
                                  public System.EventHandler M()
                                  {
                                      static System.Threading.Tasks.Task<int> Local() => System.Threading.Tasks.Task.FromResult(1);
                                      return async {|PSH1000:(sender, e)|} => await Local();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.EventHandler M()
                                       {
                                           static System.Threading.Tasks.Task<int> Local() => System.Threading.Tasks.Task.FromResult(1);
                                           return static async (sender, e) => await Local();
                                       }
                                   }
                                   """;
        var test = new VerifyStaticFunction.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = Source, FixedCode = FixedSource };
        await test.RunAsync(cancellationToken);
    }
}
