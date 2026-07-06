// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyTryGetValue = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1104UseTryGetValueAnalyzer,
    PerformanceSharp.Analyzers.Psh1104UseTryGetValueCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1104 (use TryGetValue instead of ContainsKey plus an indexer read) and its code fix.</summary>
public class UseTryGetValueAnalyzerUnitTest
{
    /// <summary>Verifies an if guard with a single indexer read is reported (PSH1104) and rewritten to TryGetValue.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IfGuardWithSingleReadRewrittenAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int M(Dictionary<string, int> map, string key)
                                  {
                                      if (map.{|PSH1104:ContainsKey|}(key))
                                      {
                                          return map[key];
                                      }

                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public int M(Dictionary<string, int> map, string key)
                                       {
                                           if (map.TryGetValue(key, out var value))
                                           {
                                               return value;
                                           }

                                           return 0;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a guard that is the leftmost operand of an &amp;&amp; chain is reported and its chained read replaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IfGuardLeftmostInAndChainRewrittenAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(Dictionary<string, int> map, string key)
                                  {
                                      if (map.{|PSH1104:ContainsKey|}(key) && map[key] > 3)
                                      {
                                          System.Console.WriteLine(map[key]);
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
                                           if (map.TryGetValue(key, out var value) && value > 3)
                                           {
                                               System.Console.WriteLine(value);
                                           }
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies every guarded indexer read in the body is replaced by the out variable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleGuardedReadsAllReplacedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int M(Dictionary<string, int> map, string key)
                                  {
                                      if (map.{|PSH1104:ContainsKey|}(key))
                                      {
                                          var total = map[key] + map[key];
                                          return total + map[key];
                                      }

                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public int M(Dictionary<string, int> map, string key)
                                       {
                                           if (map.TryGetValue(key, out var value))
                                           {
                                               var total = value + value;
                                               return total + value;
                                           }

                                           return 0;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a guarded indexer write suppresses the rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedIndexerWriteIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(Dictionary<string, int> map, string key)
                                  {
                                      if (map.ContainsKey(key))
                                      {
                                          map[key] = map[key] + 1;
                                      }

                                      if (map.ContainsKey(key))
                                      {
                                          map[key]++;
                                      }
                                  }
                              }
                              """;
        await VerifyCleanNet90Async(Source);
    }

    /// <summary>Verifies a guarded read with a different key expression is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentKeyExpressionIsCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public void M(Dictionary<string, int> map, string key, string other)
                                  {
                                      if (map.ContainsKey(key))
                                      {
                                          System.Console.WriteLine(map[other]);
                                      }
                                  }
                              }
                              """;
        await VerifyCleanNet90Async(Source);
    }

    /// <summary>Verifies the ternary shape is reported and its true branch read replaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TernaryShapeRewrittenAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int M(Dictionary<string, int> map, string key)
                                  {
                                      return map.{|PSH1104:ContainsKey|}(key) ? map[key] : -1;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public int M(Dictionary<string, int> map, string key)
                                       {
                                           return map.TryGetValue(key, out var value) ? value : -1;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a custom dictionary type implementing IDictionary&lt;K, V&gt; is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomDictionaryTypeReportedAsync()
    {
        const string Source = """
                              using System.Collections;
                              using System.Collections.Generic;

                              public class CustomMap : IDictionary<string, int>
                              {
                                  private readonly Dictionary<string, int> _inner = new();

                                  public ICollection<string> Keys => _inner.Keys;

                                  public ICollection<int> Values => _inner.Values;

                                  public int Count => _inner.Count;

                                  public bool IsReadOnly => false;

                                  public int this[string key] { get => _inner[key]; set => _inner[key] = value; }

                                  public void Add(string key, int value) => _inner.Add(key, value);

                                  public void Add(KeyValuePair<string, int> item) => _inner.Add(item.Key, item.Value);

                                  public void Clear() => _inner.Clear();

                                  public bool Contains(KeyValuePair<string, int> item) => _inner.ContainsKey(item.Key);

                                  public bool ContainsKey(string key) => _inner.ContainsKey(key);

                                  public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex)
                                  {
                                  }

                                  public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => _inner.GetEnumerator();

                                  public bool Remove(string key) => _inner.Remove(key);

                                  public bool Remove(KeyValuePair<string, int> item) => _inner.Remove(item.Key);

                                  public bool TryGetValue(string key, out int value) => _inner.TryGetValue(key, out value);

                                  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                              }

                              public class C
                              {
                                  public int M(CustomMap map, string key)
                                  {
                                      if (map.{|PSH1104:ContainsKey|}(key))
                                      {
                                          return map[key];
                                      }

                                      return 0;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections;
                                   using System.Collections.Generic;

                                   public class CustomMap : IDictionary<string, int>
                                   {
                                       private readonly Dictionary<string, int> _inner = new();

                                       public ICollection<string> Keys => _inner.Keys;

                                       public ICollection<int> Values => _inner.Values;

                                       public int Count => _inner.Count;

                                       public bool IsReadOnly => false;

                                       public int this[string key] { get => _inner[key]; set => _inner[key] = value; }

                                       public void Add(string key, int value) => _inner.Add(key, value);

                                       public void Add(KeyValuePair<string, int> item) => _inner.Add(item.Key, item.Value);

                                       public void Clear() => _inner.Clear();

                                       public bool Contains(KeyValuePair<string, int> item) => _inner.ContainsKey(item.Key);

                                       public bool ContainsKey(string key) => _inner.ContainsKey(key);

                                       public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex)
                                       {
                                       }

                                       public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => _inner.GetEnumerator();

                                       public bool Remove(string key) => _inner.Remove(key);

                                       public bool Remove(KeyValuePair<string, int> item) => _inner.Remove(item.Key);

                                       public bool TryGetValue(string key, out int value) => _inner.TryGetValue(key, out value);

                                       IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                                   }

                                   public class C
                                   {
                                       public int M(CustomMap map, string key)
                                       {
                                           if (map.TryGetValue(key, out var value))
                                           {
                                               return value;
                                           }

                                           return 0;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a receiver whose type exposes no TryGetValue is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReceiverWithoutTryGetValueIsCleanAsync()
    {
        const string Source = """
                              public class KeyedBag
                              {
                                  public bool ContainsKey(string key) => key.Length > 0;

                                  public int this[string key] => key.Length;
                              }

                              public class C
                              {
                                  public int M(KeyedBag bag, string key)
                                  {
                                      if (bag.ContainsKey(key))
                                      {
                                          return bag[key];
                                      }

                                      return 0;
                                  }
                              }
                              """;
        await VerifyCleanNet90Async(Source);
    }

    /// <summary>Verifies the fix falls back to <c>dictValue</c> when the enclosing member already uses <c>value</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExistingValueIdentifierUsesFallbackNameAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int M(Dictionary<string, int> map, string key)
                                  {
                                      var value = 10;
                                      if (map.{|PSH1104:ContainsKey|}(key))
                                      {
                                          return map[key] + value;
                                      }

                                      return value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public int M(Dictionary<string, int> map, string key)
                                       {
                                           var value = 10;
                                           if (map.TryGetValue(key, out var dictValue))
                                           {
                                               return dictValue + value;
                                           }

                                           return value;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every guard in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class A
                              {
                                  public int M(Dictionary<string, int> map, string key)
                                  {
                                      if (map.{|PSH1104:ContainsKey|}(key))
                                      {
                                          return map[key];
                                      }

                                      return 0;
                                  }
                              }

                              public class B
                              {
                                  public int M(Dictionary<string, int> map, string key)
                                  {
                                      return map.{|PSH1104:ContainsKey|}(key) ? map[key] : -1;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class A
                                   {
                                       public int M(Dictionary<string, int> map, string key)
                                       {
                                           if (map.TryGetValue(key, out var value))
                                           {
                                               return value;
                                           }

                                           return 0;
                                       }
                                   }

                                   public class B
                                   {
                                       public int M(Dictionary<string, int> map, string key)
                                       {
                                           return map.TryGetValue(key, out var value) ? value : -1;
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the rule stays silent below C# 7, where the 'out var' declaration the fix emits does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenLanguageVersionBelowCSharp7Async()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public int M(Dictionary<string, int> map, string key)
                                  {
                                      if (map.ContainsKey(key))
                                      {
                                          return map[key];
                                      }

                                      return 0;
                                  }
                              }
                              """;

        var test = new VerifyTryGetValue.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = Source, FixedCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp6));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyTryGetValue.Test
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
        var test = new VerifyTryGetValue.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
