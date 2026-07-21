// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDelegateCreation = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2258RemoveRedundantDelegateCreationAnalyzer,
    StyleSharp.Analyzers.Sst2258RemoveRedundantDelegateCreationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2258RemoveRedundantDelegateCreationAnalyzer"/> and its code fix (SST2258).</summary>
public class RemoveRedundantDelegateCreationAnalyzerUnitTest
{
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
    [Test]
    public async Task ArgumentWrapperIsCleanAsync()
        => await VerifyDelegateCreation.VerifyAnalyzerAsync(
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
    [Test]
    public async Task DelegateCombinationOperandIsCleanAsync()
        => await VerifyDelegateCreation.VerifyAnalyzerAsync(
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
    [Test]
    public async Task LambdaWrapperIsCleanAsync()
        => await VerifyDelegateCreation.VerifyAnalyzerAsync(
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
    [Test]
    public async Task NonDelegateCreationIsCleanAsync()
        => await VerifyDelegateCreation.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            internal class C
            {
                public List<int> Make() => new List<int>(4);
            }
            """);

    /// <summary>Verifies a wrapper around an existing delegate value is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateValueWrapperIsCleanAsync()
        => await VerifyDelegateCreation.VerifyAnalyzerAsync(
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
