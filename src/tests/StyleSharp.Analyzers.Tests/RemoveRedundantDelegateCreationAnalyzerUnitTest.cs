// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using VerifyDelegateCreation = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2258RemoveRedundantDelegateCreationAnalyzer,
    StyleSharp.Analyzers.Sst2258RemoveRedundantDelegateCreationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2258RemoveRedundantDelegateCreationAnalyzer"/> and its code fix (SST2258).</summary>
public class RemoveRedundantDelegateCreationAnalyzerUnitTest
{
    /// <summary>Verifies malformed wrappers and incompatible overloaded method groups do not suggest removal.</summary>
    /// <param name="creation">The creation expression to inspect.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("new Action()")]
    [Arguments("new Action(M, M)")]
    [Arguments("new Action(M) { }")]
    [Arguments("new Action(method: M)")]
    [Arguments("new Action(ref existing)")]
    [Arguments("new Action(M)")]
    [Arguments("new Action(Missing)")]
    [Arguments("new Missing(M)")]
    [Arguments("new Action(this[null])")]
    public async Task InvalidDelegateWrapperIsIgnoredAsync(string creation)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            using System;
            class C
            {
                void M(int value) { }
                void M(string value) { }
                public Action this[string key] => null;
                public Action this[Type key] => null;
                Action Make(Action existing) => {{creation}};
            }
            """);
        var compilation = CSharpCompilation.Create(nameof(Test), [tree], RuntimeMetadataReferences.Platform, new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Sst2258RemoveRedundantDelegateCreationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies return, arrow, cast, and assignment contexts provide a delegate target.</summary>
    /// <param name="member">The member containing the reported wrapper.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Action Make() => {|SST2258:new Action(M)|};")]
    [Arguments("Action Make() { return {|SST2258:new Action(M)|}; }")]
    [Arguments("object Make() => (Action){|SST2258:new Action(M)|};")]
    [Arguments("void Set(Action value) { value = {|SST2258:new Action(M)|}; }")]
    [Arguments("void Set(Action value) { value -= {|SST2258:new Action(M)|}; }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task TargetTypedWrapperIsReportedAsync(string member) =>
        VerifyDelegateCreation.VerifyAnalyzerAsync($"using System; class C {{ void M() {{ }} {member} }}");

    /// <summary>Verifies a delegate wrapper in a local initializer is reported and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializerWrapperIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  public Action Make()
                                  {
                                      Action a = {|SST2258:new Action(OnChanged)|};
                                      return a;
                                  }

                                  private void OnChanged()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       public Action Make()
                                       {
                                           Action a = OnChanged;
                                           return a;
                                       }

                                       private void OnChanged()
                                       {
                                       }
                                   }
                                   """;
        await VerifyDelegateCreation.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a delegate wrapper on the right of a compound assignment is reported and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddAssignmentWrapperIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  public Action Make()
                                  {
                                      Action a = OnChanged;
                                      a += {|SST2258:new Action(OnOther)|};
                                      return a;
                                  }

                                  private void OnChanged()
                                  {
                                  }

                                  private void OnOther()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       public Action Make()
                                       {
                                           Action a = OnChanged;
                                           a += OnOther;
                                           return a;
                                       }

                                       private void OnChanged()
                                       {
                                       }

                                       private void OnOther()
                                       {
                                       }
                                   }
                                   """;
        await VerifyDelegateCreation.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a wrapper in an argument position is left alone; a method group is not always re-bindable there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ArgumentWrapperIsCleanAsync() =>
        VerifyDelegateCreation.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                public void Use()
                {
                    Register(new Action(OnChanged));
                }

                private void Register(Action action)
                {
                }

                private void OnChanged()
                {
                }
            }
            """);

    /// <summary>Verifies a wrapper on the right of an operand of delegate combination is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DelegateCombinationOperandIsCleanAsync() =>
        VerifyDelegateCreation.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                public Action Make(Action first)
                {
                    return first + new Action(OnChanged);
                }

                private void OnChanged()
                {
                }
            }
            """);

    /// <summary>Verifies a wrapper around a lambda, not a method group, is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LambdaWrapperIsCleanAsync() =>
        VerifyDelegateCreation.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                public Action Make()
                {
                    Action a = new Action(() => { });
                    return a;
                }
            }
            """);

    /// <summary>Verifies a non-delegate object creation is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonDelegateCreationIsCleanAsync() =>
        VerifyDelegateCreation.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            internal class C
            {
                public List<int> Make() => new List<int>(4);
            }
            """);

    /// <summary>Verifies a wrapper around an existing delegate value is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DelegateValueWrapperIsCleanAsync() =>
        VerifyDelegateCreation.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                public Action Make(Action existing)
                {
                    Action a = new Action(existing);
                    return a;
                }
            }
            """);
}
