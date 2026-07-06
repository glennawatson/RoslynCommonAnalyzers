// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyModernSyntaxFlow = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ModernSyntaxFlowAnalyzer,
    StyleSharp.Analyzers.ModernSyntaxFlowCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for flow-shaped modern syntax rules (SST2207/SST2208).</summary>
public class ModernSyntaxFlowAnalyzerUnitTest
{
    /// <summary>Verifies a null guard plus return can use a throw expression.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullGuardReturnCandidateIsFixedAsync()
    {
        const string Source = """
                              #nullable enable

                              using System;

                              public sealed class C
                              {
                                  public string M(string? value)
                                  {
                                      {|SST2207:if|} (value is null)
                                      {
                                          throw new ArgumentNullException(nameof(value));
                                      }

                                      return value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable

                                   using System;

                                   public sealed class C
                                   {
                                       public string M(string? value)
                                       {
                                           return value ?? throw new ArgumentNullException(nameof(value));
                                       }
                                   }
                                   """;
        var test = new VerifyModernSyntaxFlow.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an out local declared for the next statement is moved to the out argument.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OutVariableDeclarationCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(string text)
                                  {
                                      int {|SST2208:value|};
                                      if (int.TryParse(text, out value))
                                      {
                                          return value;
                                      }

                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(string text)
                                       {
                                           if (int.TryParse(text, out var value))
                                           {
                                               return value;
                                           }

                                           return 0;
                                       }
                                   }
                                   """;
        var test = new VerifyModernSyntaxFlow.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies guarded shapes with extra work stay clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonCandidatesAreCleanAsync()
    {
        const string Source = """
                              #nullable enable

                              using System;

                              public sealed class C
                              {
                                  public string M(string? value)
                                  {
                                      if (value is null)
                                      {
                                          throw new ArgumentNullException(nameof(value));
                                      }

                                      return value.ToString();
                                  }

                                  public int Parse(string text)
                                  {
                                      int value = 0;
                                      if (int.TryParse(text, out value))
                                      {
                                          return value;
                                      }

                                      return value;
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxFlow.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an out local is not inlined into a nested block when later code still needs the outer scope.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NestedOutVariableUsedAfterNextStatementIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly object gate = new();

                                  private void RunDue()
                                  {
                                      while (true)
                                      {
                                          TimedWorkItem next;
                                          lock (gate)
                                          {
                                              if (!TryDequeueDueNoLock(out next))
                                              {
                                                  ArmTimerNoLock();
                                                  return;
                                              }
                                          }

                                          ExecuteQueued(next.Item);
                                      }
                                  }

                                  private static void ArmTimerNoLock()
                                  {
                                  }

                                  private static void ExecuteQueued(object item)
                                  {
                                  }

                                  private static bool TryDequeueDueNoLock(out TimedWorkItem item)
                                  {
                                      item = new TimedWorkItem();
                                      return true;
                                  }

                                  private readonly struct TimedWorkItem
                                  {
                                      public object Item => new();
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxFlow.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an out local is not inlined into a loop condition when later code still needs the outer scope.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LoopConditionOutVariableUsedAfterLoopIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public void M(Dictionary<string, int> counts, string keyName)
                                  {
                                      int value;
                                      while (counts.TryGetValue(keyName, out value))
                                      {
                                          keyName = $"{keyName}{++value}";
                                      }

                                      counts[keyName] = value;
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxFlow.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the inline out-variable rule stays silent below C# 7, where inline out declarations do not exist.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InlineOutVariableIsSilentBelowCSharp7Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(string text)
                                  {
                                      int value;
                                      if (int.TryParse(text, out value))
                                      {
                                          return value;
                                      }

                                      return 0;
                                  }
                              }
                              """;
        var test = new VerifyModernSyntaxFlow.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = Source
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp6));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
