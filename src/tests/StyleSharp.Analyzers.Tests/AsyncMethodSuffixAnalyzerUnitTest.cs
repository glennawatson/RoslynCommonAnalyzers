// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyAsyncSuffix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.MethodNamingAnalyzer,
    StyleSharp.Analyzers.NamingRenameCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1317 (async method naming) and its rename fix.</summary>
public class AsyncMethodSuffixAnalyzerUnitTest
{
    /// <summary>Verifies a method misplaced in a namespace still binds, so its missing suffix is reported and renamed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MisplacedNamespaceMethodIsReportedAsync()
    {
        var test = new VerifyAsyncSuffix.Test
        {
            CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.None,
            TestCode = "namespace N { System.Threading.Tasks.Task {|SST1317:Load|}() => null; }",
            FixedCode = "namespace N { System.Threading.Tasks.Task LoadAsync() => null; }",
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies qualification and generic task return types retain the suffix requirement.</summary>
    /// <param name="returnType">The fully qualified task return type.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("System.Threading.Tasks.Task")]
    [Arguments("System.Threading.Tasks.Task<int>")]
    [Arguments("System.Threading.Tasks.ValueTask")]
    [Arguments("System.Threading.Tasks.ValueTask<int>")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task QualifiedTaskReturnsRequireSuffixAsync(string returnType) =>
        VerifyAsyncSuffix.VerifyAnalyzerAsync($"class C {{ public {returnType} {{|SST1317:Load|}}() => default; }}");

    /// <summary>Verifies task methods constrained by base and interface contracts retain their names.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task TaskMethodContractsKeepTheirNamesAsync() =>
        VerifyAsyncSuffix.VerifyAnalyzerAsync("""
            using System.Threading.Tasks;
            interface IRunner { Task {|SST1317:Run|}(int count); }
            abstract class Base { public abstract Task {|SST1317:Load|}(); }
            class Implicit : Base, IRunner
            {
                public override Task Load() => Task.CompletedTask;
                public Task Run(int count) => Task.CompletedTask;
            }
            class Explicit : IRunner
            {
                Task IRunner.Run(int {|SST1318:value|}) => Task.CompletedTask;
            }
            """);

    /// <summary>Verifies task lookalikes from other namespace hierarchies are not asynchronous returns.</summary>
    /// <param name="declaration">The namespace containing the lookalike task.</param>
    /// <param name="type">The qualified lookalike name.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("namespace Other { public class Task { } }", "Other.Task")]
    [Arguments("namespace Other.Tasks { public class Task { } }", "Other.Tasks.Task")]
    [Arguments("namespace Other.Threading.Tasks { public class Task { } }", "Other.Threading.Tasks.Task")]
    [Arguments("public class Task { }", "Task")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task TaskLookalikesAreCleanAsync(string declaration, string type) =>
        VerifyAsyncSuffix.VerifyAnalyzerAsync($"{declaration}\nclass C {{ public {type} Load() => null; }}");

    /// <summary>Verifies a task-returning method without the suffix is reported and renamed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TaskMethodWithoutSuffixRenamedAsync()
    {
        const string Source = """
                              using System.Threading.Tasks;

                              public class C
                              {
                                  public async Task {|SST1317:Load|}()
                                  {
                                      await Task.Delay(1);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public async Task LoadAsync()
                                       {
                                           await Task.Delay(1);
                                       }
                                   }
                                   """;
        await VerifyAsyncSuffix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a suffixed method, an async void handler, and a non-task method are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NamedCorrectlyOrNonTaskAreCleanAsync() =>
        VerifyAsyncSuffix.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public Task SaveAsync() => Task.CompletedTask;

                public async void OnClick()
                {
                    await Task.Delay(1);
                }

                public int Compute() => 0;
            }
            """);
}
