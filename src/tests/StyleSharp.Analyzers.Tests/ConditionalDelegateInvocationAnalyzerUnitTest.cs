// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyConditionalDelegateInvocation = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2240ConditionalDelegateInvocationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2240ConditionalDelegateInvocationAnalyzer"/>.</summary>
public class ConditionalDelegateInvocationAnalyzerUnitTest
{
    /// <summary>Verifies a null-checked delegate that is immediately invoked is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullCheckedDelegateInvocationIsReportedAsync() =>
        RunAsync(
            """
            using System;

            public sealed class C
            {
                private Action _changed = null!;

                public void M()
                {
                    {|SST2240:if|} (_changed != null)
                    {
                        _changed();
                    }
                }
            }
            """);

    /// <summary>Verifies a null check that does not invoke the checked delegate is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonInvocationGuardIsCleanAsync() =>
        RunAsync(
            """
            using System;

            public sealed class C
            {
                public void M(Action callback)
                {
                    if (callback != null)
                    {
                        Console.WriteLine(1);
                    }
                }
            }
            """);

    /// <summary>Verifies the rule stays silent below C# 6, where null-conditional invocation does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp6Async()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public void M(Action changed)
                                  {
                                      if (changed != null)
                                      {
                                          changed();
                                      }
                                  }
                              }
                              """;
        var test = new VerifyConditionalDelegateInvocation.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp5));
        });
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies supported null checks and invocation spellings report the same delegate.</summary>
    /// <param name="condition">The null guard.</param>
    /// <param name="body">The single invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("null != callback", "callback();")]
    [Arguments("((callback) != (null))", "{ callback.Invoke(); }")]
    [Arguments("callback is not null", "callback.Invoke();")]
    public Task AlternateNullGuardsAreReportedAsync(string condition, string body) =>
        RunAsync($$"""
            class C
            {
                void M(System.Action callback)
                {
                    {|SST2240:if|} ({{condition}}) {{body}}
                }
            }
            """);

    /// <summary>Verifies near-miss conditions and bodies preserve their existing control flow.</summary>
    /// <param name="statement">The guarded statement that cannot be simplified.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("if (callback != null) callback(); else other();")]
    [Arguments("if (callback == null) callback();")]
    [Arguments("if (callback != other) callback();")]
    [Arguments("if (callback is null) callback();")]
    [Arguments("if (callback is not System.Action) callback();")]
    [Arguments("if (value is not 0) callback();")]
    [Arguments("if (callback != null) { }")]
    [Arguments("if (callback != null) { callback(); other(); }")]
    [Arguments("if (callback != null) return;")]
    [Arguments("if (callback != null) { return; }")]
    [Arguments("if (callback != null) { value = 1; }")]
    [Arguments("if (callback != null) value = 1;")]
    [Arguments("if (callback != null) other();")]
    [Arguments("if (callback != null) callback.ToString();")]
    public Task UnsupportedGuardsAreCleanAsync(string statement) =>
        RunAsync($$"""
            class C
            {
                void M(System.Action callback, System.Action other, int value)
                {
                    {{statement}}
                }
            }
            """);

    /// <summary>Verifies repeated factory calls currently match by method symbol despite evaluating separately.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RepeatedDelegateFactoryCallsAreReportedAsync() =>
        RunAsync("""
            class C
            {
                System.Action GetCallback() => () => { };
                void M()
                {
                    {|SST2240:if|} (GetCallback() != null) GetCallback()();
                }
            }
            """);

    /// <summary>Verifies an Invoke method on a nondelegate is not treated as delegate invocation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NondelegateInvokeIsCleanAsync() =>
        RunAsync("class C { public void Invoke() { } void M(C callback) { if (callback != null) callback.Invoke(); } }");

    /// <summary>Verifies unresolved symbols and untyped method groups do not produce a diagnostic.</summary>
    /// <param name="source">The invalid delegate guard.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class C { void M() { if (missing != null) missing(); } }")]
    [Arguments("class C { void M() { if (M != null) M(); } }")]
    [Arguments("class C { void M() { if (System != null) System(); } }")]
    public Task UnresolvedOrUntypedDelegateIsCleanAsync(string source) =>
        new VerifyConditionalDelegateInvocation.Test { TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None }.RunAsync(CancellationToken.None);

    /// <summary>Verifies nullable generic delegate properties retain their symbol identity.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullableGenericDelegatePropertyIsReportedAsync() =>
        RunAsync("""
            #nullable enable
            class C<T>
            {
                System.Action<T>? Callback { get; set; }
                void M(T value)
                {
                    {|SST2240:if|} (Callback is not null) Callback.Invoke(value);
                }
            }
            """);

    /// <summary>Runs the analyzer verifier with modern reference assemblies.</summary>
    /// <param name="source">The source code to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task RunAsync(string source) =>
        new VerifyConditionalDelegateInvocation.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = source }.RunAsync(CancellationToken.None);
}
