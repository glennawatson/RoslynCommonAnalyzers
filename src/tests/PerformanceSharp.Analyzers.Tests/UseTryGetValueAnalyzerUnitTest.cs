// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyTryGetValue = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1104UseTryGetValueAnalyzer,
    PerformanceSharp.Analyzers.Psh1104UseTryGetValueCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1104 (use TryGetValue instead of ContainsKey plus an indexer read) and its code fix.</summary>
public class UseTryGetValueAnalyzerUnitTest
{
    /// <summary>A ContainsKey guard plus an indexer read on a user-defined type that implements <c>IDictionary&lt;string, int&gt;</c>.</summary>
    private const string CustomDictionaryTypeSource = """
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

    /// <summary>The same custom-dictionary guard once the fix folds the ContainsKey call and the indexer read into TryGetValue.</summary>
    private const string CustomDictionaryTypeFixedSource = """
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

    /// <summary>Verifies mutations through the indexer prevent replacing its reads with a snapshot.</summary>
    /// <param name="statement">The statement that writes the guarded element.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("++map[key];")]
    [Arguments("--map[key];")]
    [Arguments("map[key]--;")]
    [Arguments("map[key] += 1;")]
    [Arguments("_ = map[key]; map[key] = 1;")]
    [Arguments("Write(ref map[key]);")]
    [Arguments("Output(out map[key]);")]
    [Arguments("ref int alias = ref map[key];")]
    [Arguments("(map[key], other) = (1, 2);")]
    [Arguments("((map[key], other), other) = ((1, 2), 3);")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task GuardedWritesPreventDiagnosticAsync(string statement) =>
        VerifyLookupAsync($"if (map.ContainsKey(key)) {{ {statement} return map[key]; }} return 0;");

    /// <summary>Verifies read-only uses inside assignments, unary expressions, and tuples remain eligible.</summary>
    /// <param name="expression">The read expression in the guarded return.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("other = map[key]")]
    [Arguments("-map[key]")]
    [Arguments("map[key]!")]
    [Arguments("Read(in map[key])")]
    [Arguments("(map[key], other).Item1")]
    [Arguments("((map[key], other), other).Item1.Item1")]
    [Arguments("(other, other) = (map[key], 0)")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task GuardedReadExpressionsAreReportedAsync(string expression) =>
        VerifyLookupAsync($"if (map.{{|PSH1104:ContainsKey|}}(key)) {{ _ = {expression}; return map[key]; }} return 0;");

    /// <summary>Verifies unsupported guard locations and argument shapes stay silent.</summary>
    /// <param name="body">The method body containing the candidate guard.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("if (other > 0 && map.ContainsKey(key)) return map[key]; return 0;")]
    [Arguments("if (map.ContainsKey(key) || other > 0) return map[key]; return 0;")]
    [Arguments("if (map.ContainsKey(key)) return map[other]; return 0;")]
    [Arguments("if (map.ContainsKey(key)) return alternate[key]; return 0;")]
    [Arguments("if (map.ContainsKey(key)) return map[key, other]; return 0;")]
    [Arguments("if (map.ContainsKey(key)) return 1; return 0;")]
    [Arguments("if (map.ContainsKey(key: key)) return map[key]; return 0;")]
    [Arguments("if (map.ContainsKey(key + 1)) return map[key + 1]; return 0;")]
    [Arguments("if (GetMap().ContainsKey(key)) return GetMap()[key]; return 0;")]
    [Arguments("return map.ContainsKey(key) && other > 0 ? map[key] : 0;")]
    [Arguments("if (other > 0 ? map.ContainsKey(key) : true) return map[key]; return 0;")]
    [Arguments("return other > 0 ? map.ContainsKey(key) ? 1 : 0 : map[key];")]
    [Arguments("if (map.ContainsKey(key) && ++map[key] > 0) return map[key]; return 0;")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnsupportedGuardShapesAreCleanAsync(string body) => VerifyLookupAsync(body);

    /// <summary>Verifies syntactically plausible calls with no usable method binding stay silent.</summary>
    /// <param name="receiverType">The receiver's declared type.</param>
    /// <param name="guard">The candidate invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Missing", "map.ContainsKey(key)")]
    [Arguments("dynamic", "map.ContainsKey(key)")]
    [Arguments("Map", "map.ContainsKey()")]
    [Arguments("Map", "map.ContainsKey(key, key)")]
    [Arguments("Map", "map.ContainsKey(ref key)")]
    public Task UnresolvedGuardIsCleanAsync(string receiverType, string guard)
    {
        var test = new CSharpAnalyzerVerifier<Psh1104UseTryGetValueAnalyzer>.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = $$"""
                class Map
                {
                    public int this[int key] => 0;
                    public bool TryGetValue(int key, out int value) { value = 0; return true; }
                }
                class C { int M({{receiverType}} map, int key) { if ({{guard}}) return map[key]; return 0; } }
                """,
        };
        return test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the replacement requires an accessible Boolean method with an out parameter.</summary>
    /// <param name="containsKey">The guard method declaration.</param>
    /// <param name="tryGetValue">The proposed replacement method declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public bool ContainsKey(int key, int optional = 0) => true;", "public bool TryGetValue(int key, out int value) { value = 0; return true; }")]
    [Arguments("public bool ContainsKey(int key) => true;", "private bool TryGetValue(int key, out int value) { value = 0; return true; }")]
    [Arguments("public bool ContainsKey(int key) => true;", "public int TryGetValue(int key, out int value) { value = 0; return 0; }")]
    [Arguments("public bool ContainsKey(int key) => true;", "public bool TryGetValue(int key, ref int value) => true;")]
    public Task UnusableReplacementMethodIsCleanAsync(string containsKey, string tryGetValue)
    {
        var test = new CSharpAnalyzerVerifier<Psh1104UseTryGetValueAnalyzer>.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = $$"""
                class Map { public int this[int key] => 0; {{containsKey}} {{tryGetValue}} }
                class C { int M(Map map, int key) { if (map.ContainsKey(key)) return map[key]; return 0; } }
                """,
        };
        return test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies direct syntax validation rejects invocations outside the supported member-call shape.</summary>
    /// <param name="expression">The invocation syntax to inspect.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("ContainsKey(key)")]
    [Arguments("map.Other(key)")]
    [Arguments("map->ContainsKey(key)")]
    [Arguments("map.ContainsKey(ref key)")]
    public async Task UnsupportedInvocationSyntaxIsRejectedAsync(string expression)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(expression);
        await Assert.That(Psh1104UseTryGetValueAnalyzer.TryGetGuardShape(invocation, out _)).IsFalse();
    }

    /// <summary>Verifies a region consisting of a write target stops scanning immediately.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RootElementWriteDisqualifiesGuardAsync()
    {
        var assignment = (AssignmentExpressionSyntax)SyntaxFactory.ParseExpression("map[key] = 1");
        var element = (ElementAccessExpressionSyntax)assignment.Left;
        var shape = new Psh1104UseTryGetValueAnalyzer.GuardShape(element.Expression, element.ArgumentList.Arguments[0].Expression, element, null);
        await Assert.That(Psh1104UseTryGetValueAnalyzer.HasOnlyGuardedReads(shape)).IsFalse();
    }

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CustomDictionaryTypeReportedAsync() =>
        VerifyNet90Async(CustomDictionaryTypeSource, CustomDictionaryTypeFixedSource);

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

    /// <summary>Runs a lookup scenario against a minimal map with a ref-returning indexer.</summary>
    /// <param name="body">The method body under analysis.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    private static Task VerifyLookupAsync(string body)
    {
        var test = new CSharpAnalyzerVerifier<Psh1104UseTryGetValueAnalyzer>.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = $$"""
                class Map
                {
                    private int _value;
                    public ref int this[int key] => ref _value;
                    public int this[int key, int other] => 0;
                    public bool ContainsKey(int key) => true;
                    public bool TryGetValue(int key, out int value) { value = 0; return true; }
                }
                class C
                {
                    static Map GetMap() => new Map();
                    static void Write(ref int value) { }
                    static void Output(out int value) { value = 0; }
                    static int Read(in int value) => value;
                    int M(Map map, Map alternate, int key, int other) { {{body}} }
                }
                """,
        };
        return test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyTryGetValue.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanNet90Async(string source)
    {
        var test = new VerifyTryGetValue.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = source };

        await test.RunAsync(CancellationToken.None);
    }
}
