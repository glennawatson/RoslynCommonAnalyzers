// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1312ReturnCompletedTaskOverNullAnalyzer,
    PerformanceSharp.Analyzers.Psh1312ReturnCompletedTaskOverNullCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1312ReturnCompletedTaskOverNullAnalyzer"/> (PSH1312 completed task over null).</summary>
public class ReturnCompletedTaskOverNullAnalyzerUnitTest
{
    /// <summary>Verifies each supported accessor and local-function body reports null task returns.</summary>
    /// <param name="member">The declaration containing a null task.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public Task Value { get { return {|PSH1312:null|}; } }")]
    [Arguments("public Task Value { get => {|PSH1312:default(Task)|}; }")]
    [Arguments("public Task this[int i] => {|PSH1312:null|};")]
    [Arguments("public Task this[int i] { get => {|PSH1312:null|}; }")]
    [Arguments("public Task this[int i] { get { return {|PSH1312:null|}; } }")]
    [Arguments("public Task M() { Task Local() => {|PSH1312:null|}; return Local(); }")]
    [Arguments("public Task<T> M<T>() => {|PSH1312:default(Task<T>)|};")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task AccessorAndLocalTaskReturnsAreReportedAsync(string member) =>
        VerifyAsync($"using System.Threading.Tasks; class C {{ {member} }}");

    /// <summary>Verifies unsupported return owners and non-task return symbols are ignored, even in incomplete code.</summary>
    /// <param name="source">The complete or incomplete source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { void M() { return; } }")]
    [Arguments("class C { public C() { return null; } }")]
    [Arguments("class C { public C() => null; }")]
    [Arguments("class C { object Value { set { return null; } } }")]
    [Arguments("class C { object Value { set => null; } }")]
    [Arguments("class C { event System.Action Changed { get { return null; } } }")]
    [Arguments("class C { event System.Action Changed { get => null; } }")]
    [Arguments("class C { System.Func<object> M() => () => { return null; }; }")]
    [Arguments("class C { T M<T>() => default; }")]
    [Arguments("class C { int[] M() => null; }")]
    [Arguments("class C { Missing M() => null; }")]
    [Arguments("class C { async System.Threading.Tasks.Task<object> M() => null; }")]
    [Arguments("class C { void M() { async System.Threading.Tasks.Task<object> Local() { return null; } } }")]
    [Arguments("class C { void M() { async System.Threading.Tasks.Task<object> Local() => null; } }")]
    [Arguments("return null;")]
    public async Task UnsupportedReturnOwnerIsSilentAsync(string source)
    {
        var test = new Verify.Test { TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies missing task APIs prevent suggestions in an incomplete framework.</summary>
    /// <param name="taskDeclarations">The available task definitions.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("public class Task { } public class Task<T> { }")]
    [Arguments("public class Task { public static Task CompletedTask { get; } }")]
    public async Task MissingTaskApiPreventsDiagnosticAsync(string taskDeclarations)
    {
        var source = $"namespace System.Threading.Tasks {{ {taskDeclarations} }} class C {{ System.Threading.Tasks.Task M() => null; }}";
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(nameof(Test), [tree]);
        var analysis = compilation.WithAnalyzers([new Psh1312ReturnCompletedTaskOverNullAnalyzer()]);
        await Assert.That(await analysis.GetAnalyzerDiagnosticsAsync()).IsEmpty();
    }

    /// <summary>Verifies a null returned from a Task method is flagged and rewritten to the completed task.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullReturnInTaskMethodIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task M()
                                  {
                                      return {|PSH1312:null|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M()
                                       {
                                           return Task.CompletedTask;
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an expression-bodied Task method returning null is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedNullTaskIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task M() => {|PSH1312:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M() => Task.CompletedTask;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a default literal returned from a Task method is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultReturnInTaskMethodIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task M()
                                  {
                                      return {|PSH1312:default|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M()
                                       {
                                           return Task.CompletedTask;
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a null returned from a generic Task method is rewritten to a typed FromResult.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullReturnInGenericTaskMethodIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task<string> M()
                                  {
                                      return {|PSH1312:null|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<string> M()
                                       {
                                           return Task.FromResult<string>(default);
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a null returned from a Task-returning local function is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullReturnInLocalFunctionIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task M()
                                  {
                                      return Local();

                                      Task Local()
                                      {
                                          return {|PSH1312:null|};
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M()
                                       {
                                           return Local();

                                           Task Local()
                                           {
                                               return Task.CompletedTask;
                                           }
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an expression-bodied Task property returning null is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedNullTaskPropertyIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task Pending => {|PSH1312:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task Pending => Task.CompletedTask;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an async method returning a null result stays clean; that is a completed task carrying null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AsyncMethodReturningNullResultIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public async Task<string> M()
                {
                    await Task.Yield();
                    return null;
                }
            }
            """);

    /// <summary>Verifies a default returned from a ValueTask method stays clean; that is already a completed task.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ValueTaskDefaultReturnIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public ValueTask M()
                {
                    return default;
                }
            }
            """);

    /// <summary>Verifies a null returned from a string method stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StringMethodReturningNullIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public string M()
                {
                    return null;
                }
            }
            """);

    /// <summary>Verifies a Task method already returning the completed task stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CompletedTaskReturnIsCleanAsync() =>
        VerifyAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public Task M()
                {
                    return Task.CompletedTask;
                }
            }
            """);

    /// <summary>Verifies that below C# 7.1 the generic fix emits an explicit default(T) instead of the bare default literal that version cannot parse.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericFixEmitsExplicitDefaultBelowCSharp71Async()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task<string> M() { return {|PSH1312:null|}; }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<string> M() { return Task.FromResult<string>(default(string)); }
                                   }
                                   """;

        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = Source, FixedCode = FixedSource };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp7));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
