// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1302RunContinuationsAsynchronouslyAnalyzer,
    PerformanceSharp.Analyzers.Psh1302RunContinuationsAsynchronouslyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1302RunContinuationsAsynchronouslyAnalyzer"/> (PSH1302 TaskCompletionSource continuations).</summary>
public class RunContinuationsAsynchronouslyAnalyzerUnitTest
{
    /// <summary>Verifies a bare completion source is flagged and the fix appends the flag.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareCompletionSourceIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task<int> M()
                                  {
                                      var tcs = {|PSH1302:new TaskCompletionSource<int>()|};
                                      return tcs.Task;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<int> M()
                                       {
                                           var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                                           return tcs.Task;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a completion source that already passes the flag is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FlaggedCompletionSourceIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public class C
            {
                public Task<int> M()
                {
                    var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                    return tcs.Task;
                }
            }
            """);

    /// <summary>Verifies a None options constant is flagged and substituted by the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoneOptionsAreFlaggedAndSubstitutedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task<int> M()
                                  {
                                      var tcs = {|PSH1302:new TaskCompletionSource<int>(TaskCreationOptions.None)|};
                                      return tcs.Task;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<int> M()
                                       {
                                           var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                                           return tcs.Task;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies another option is flagged and or-combined by the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherOptionsAreFlaggedAndCombinedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task<int> M()
                                  {
                                      var tcs = {|PSH1302:new TaskCompletionSource<int>(TaskCreationOptions.AttachedToParent)|};
                                      return tcs.Task;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<int> M()
                                       {
                                           var tcs = new TaskCompletionSource<int>(TaskCreationOptions.AttachedToParent | TaskCreationOptions.RunContinuationsAsynchronously);
                                           return tcs.Task;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the state-only constructor is flagged and the fix appends the options argument.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StateOnlyConstructorGainsOptionsArgumentAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task<int> M(object state)
                                  {
                                      var tcs = {|PSH1302:new TaskCompletionSource<int>(state)|};
                                      return tcs.Task;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task<int> M(object state)
                                       {
                                           var tcs = new TaskCompletionSource<int>(state, TaskCreationOptions.RunContinuationsAsynchronously);
                                           return tcs.Task;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a non-constant options argument stays clean because the flag may arrive at runtime.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OpaqueOptionsVariableIsCleanAsync() =>
        VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public class C
            {
                public Task<int> M(TaskCreationOptions options)
                {
                    var tcs = new TaskCompletionSource<int>(options);
                    return tcs.Task;
                }
            }
            """);

    /// <summary>Verifies an implicit creation is flagged and fixed in place.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitCreationIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  private readonly TaskCompletionSource<bool> _tcs = {|PSH1302:new()|};

                                  public Task<bool> Task => _tcs.Task;
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

                                       public Task<bool> Task => _tcs.Task;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the non-generic completion source is flagged too.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonGenericCompletionSourceIsFlaggedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public Task M()
                                  {
                                      var tcs = {|PSH1302:new TaskCompletionSource()|};
                                      return tcs.Task;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task M()
                                       {
                                           var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                                           return tcs.Task;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies namespace qualification and reordered named arguments preserve option binding.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task QualifiedAndNamedConstructorArgumentsAreBoundAsync() =>
        VerifyNet90Async("""
            using System.Threading.Tasks;
            class C
            {
                object M(object state)
                {
                    _ = {|PSH1302:new System.Threading.Tasks.TaskCompletionSource<int>()|};
                    _ = {|PSH1302:new TaskCompletionSource<int>(state: state, creationOptions: TaskCreationOptions.None)|};
                    _ = {|PSH1302:new TaskCompletionSource<int>(creationOptions: TaskCreationOptions.None, state: state)|};
                    _ = new TaskCompletionSource<int>(state: state, creationOptions: TaskCreationOptions.RunContinuationsAsynchronously | TaskCreationOptions.AttachedToParent);
                    return new object();
                }
            }
            """);

    /// <summary>Verifies syntax lookalikes and unresolved constructors are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnrelatedAndUnresolvedCreationsAreIgnoredAsync()
    {
        const string Source = """
            class TaskCompletionSource { }
            class TaskCompletionSource<T> { }
            class C
            {
                object M()
                {
                    _ = new global::TaskCompletionSource();
                    _ = new TaskCompletionSource<int>();
                    _ = new System.Threading.Tasks.TaskCompletionSource<int>(missing: true);
                    object other = new();
                    return new int();
                }
            }
            """;
        var compilation = CSharpCompilation.Create(nameof(Test), [CSharpSyntaxTree.ParseText(Source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1302RunContinuationsAsynchronouslyAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies missing framework types and omitted optional arguments are conservatively ignored.</summary>
    /// <param name="declarations">The minimal completion-source API present in the compilation.</param>
    /// <param name="creation">The constructor invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class TaskCompletionSource { }", "new TaskCompletionSource()")]
    [Arguments("class TaskCompletionSource<T> { }", "new TaskCompletionSource<int>()")]
    [Arguments("enum TaskCreationOptions { None = 0 } class TaskCompletionSource<T> { public TaskCompletionSource(TaskCreationOptions options = 0) { } }", "new TaskCompletionSource<int>()")]
    [Arguments("enum TaskCreationOptions { None = 0 } class TaskCompletionSource<T> { public TaskCompletionSource(TaskCreationOptions options = 0) { } }", "new TaskCompletionSource<int> { }")]
    [Arguments(
        "enum TaskCreationOptions { None = 0 } class TaskCompletionSource<T> { public TaskCompletionSource(int state = 0, TaskCreationOptions options = 0) { } }",
        "new TaskCompletionSource<int>(state: 1)")]
    public async Task MissingFrameworkOrOmittedOptionsAreIgnoredAsync(string declarations, string creation)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            namespace System { public class Object { } public class ValueType { } public class Enum { } public struct Void { } public struct Int32 { } }
            namespace System.Threading.Tasks
            {
                {{declarations}}
                class C { object M() => {{creation}}; }
            }
            """);
        var compilation = CSharpCompilation.Create(nameof(Test), [tree]);
        var expression = (await tree.GetRootAsync()).DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Single();
        await Assert.That(compilation.GetSemanticModel(tree).GetSymbolInfo(expression).Symbol).IsNotNull();
        var diagnostics = await compilation.WithAnalyzers([new Psh1302RunContinuationsAsynchronouslyAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies a lookalike is ignored on frameworks without nongeneric completion sources.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LookalikeOnOlderFrameworkIsIgnoredAsync()
    {
        var test = new Verify.Test { ReferenceAssemblies = AnalyzerFrameworks.NetStandard20, TestCode = "class TaskCompletionSource { } class C { object M() => new TaskCompletionSource(); }" };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
