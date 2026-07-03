// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyDoubleLookup = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1105AvoidDoubleLookupAnalyzer,
    PerformanceSharp.Analyzers.Psh1105AvoidDoubleLookupCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1105 (avoid double lookups on dictionaries and sets) and its code fix.</summary>
public class AvoidDoubleLookupAnalyzerUnitTest
{
    /// <summary>Verifies a ContainsKey guard around Remove is reported (PSH1105) and collapsed to the Remove call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsKeyGuardAroundRemoveCollapsedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(Dictionary<string, int> map, string key)
                                  {
                                      if (map.{|PSH1105:ContainsKey|}(key))
                                      {
                                          map.Remove(key);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public void M(Dictionary<string, int> map, string key)
                                       {
                                           map.Remove(key);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a negated ContainsKey guard around a two-argument Add is rewritten to TryAdd.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegatedContainsKeyGuardAroundAddUsesTryAddAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(Dictionary<string, int> map, string key)
                                  {
                                      if (!map.{|PSH1105:ContainsKey|}(key))
                                      {
                                          map.Add(key, 42);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public void M(Dictionary<string, int> map, string key)
                                       {
                                           map.TryAdd(key, 42);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a negated Contains guard around a set Add is collapsed to the Add call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegatedContainsGuardAroundSetAddCollapsedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(HashSet<string> items, string item)
                                  {
                                      if (!items.{|PSH1105:Contains|}(item))
                                      {
                                          items.Add(item);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public void M(HashSet<string> items, string item)
                                       {
                                           items.Add(item);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a Contains guard around a set Remove is collapsed to the Remove call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsGuardAroundSetRemoveCollapsedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(HashSet<string> items, string item)
                                  {
                                      if (items.{|PSH1105:Contains|}(item))
                                      {
                                          items.Remove(item);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public void M(HashSet<string> items, string item)
                                       {
                                           items.Remove(item);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a guard without a block body is also collapsed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardWithoutBlockBodyCollapsedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(HashSet<string> items, string item)
                                  {
                                      if (items.{|PSH1105:Contains|}(item)) items.Remove(item);
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public void M(HashSet<string> items, string item)
                                       {
                                           items.Remove(item);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a guard with an else clause is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardWithElseClauseIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(Dictionary<string, int> map, string key)
                                  {
                                      if (map.ContainsKey(key))
                                      {
                                          map.Remove(key);
                                      }
                                      else
                                      {
                                          map.Add(key, 1);
                                      }
                                  }
                              }
                              """;
        await VerifyCleanNet90Async(Source);
    }

    /// <summary>Verifies a guard whose body has two statements is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardWithTwoBodyStatementsIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(Dictionary<string, int> map, string key)
                                  {
                                      if (map.ContainsKey(key))
                                      {
                                          map.Remove(key);
                                          System.Console.WriteLine(key);
                                      }
                                  }
                              }
                              """;
        await VerifyCleanNet90Async(Source);
    }

    /// <summary>Verifies a guard and body using different keys is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentKeyBetweenGuardAndBodyIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(Dictionary<string, int> map, string key, string other)
                                  {
                                      if (map.ContainsKey(key))
                                      {
                                          map.Remove(other);
                                      }
                                  }
                              }
                              """;
        await VerifyCleanNet90Async(Source);
    }

    /// <summary>Verifies a List&lt;T&gt; Contains guard around its void-returning Add is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListContainsGuardAroundVoidAddIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(List<string> items, string item)
                                  {
                                      if (!items.Contains(item))
                                      {
                                          items.Add(item);
                                      }
                                  }
                              }
                              """;
        await VerifyCleanNet90Async(Source);
    }

    /// <summary>Verifies the guarded Add pairing stays silent when the receiver's type exposes no TryAdd.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedAddWithoutTryAddIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class NoTryAddMap
                              {
                                  private readonly Dictionary<string, int> _inner = new();

                                  public bool ContainsKey(string key) => _inner.ContainsKey(key);

                                  public void Add(string key, int value) => _inner.Add(key, value);
                              }

                              public class C
                              {
                                  public void M(NoTryAddMap map, string key)
                                  {
                                      if (!map.ContainsKey(key))
                                      {
                                          map.Add(key, 42);
                                      }
                                  }
                              }
                              """;
        await VerifyCleanNet90Async(Source);
    }

    /// <summary>Verifies Fix All collapses every redundant guard in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class A
                              {
                                  public void M(Dictionary<string, int> map, string key)
                                  {
                                      if (!map.{|PSH1105:ContainsKey|}(key))
                                      {
                                          map.Add(key, 42);
                                      }
                                  }
                              }

                              public class B
                              {
                                  public void M(HashSet<string> items, string item)
                                  {
                                      if (items.{|PSH1105:Contains|}(item))
                                      {
                                          items.Remove(item);
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class A
                                   {
                                       public void M(Dictionary<string, int> map, string key)
                                       {
                                           map.TryAdd(key, 42);
                                       }
                                   }

                                   public class B
                                   {
                                       public void M(HashSet<string> items, string item)
                                       {
                                           items.Remove(item);
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyDoubleLookup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanNet90Async(string source)
    {
        var test = new VerifyDoubleLookup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
