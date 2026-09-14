// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using VerifyLinqUsage = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.LinqUsageAnalyzer,
    PerformanceSharp.Analyzers.LinqUsageCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for the LINQ usage rules (PSH1100, PSH1101, PSH1102) and their fixes.</summary>
public class LinqUsageAnalyzerUnitTest
{
    /// <summary>The analyzer configuration path shared by source and fixed documents.</summary>
    private const string EditorConfigPath = "/.editorconfig";

    /// <summary>Verifies a Where predicate is carried by the terminal LINQ call (PSH1101).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WhereAnyChainIsCollapsedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public bool M(int[] values) => values.Where(value => value > 0).{|PSH1101:Any|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public bool M(int[] values) => values.Any(value => value > 0);
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a Where predicate moves into a terminal Count call (PSH1101).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WhereCountChainIsCollapsedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public int M(int[] values) => values.Where(value => value > 0).{|PSH1101:Count|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public int M(int[] values) => values.Count(value => value > 0);
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a LINQ type check followed by Cast is represented as one typed filter (PSH1102).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WhereTypeCheckCastChainUsesOneTypedFilterAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(object[] values) => values.Where(value => value is string).{|PSH1102:Cast<string>|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Linq;

                                   public sealed class C
                                   {
                                       public object M(object[] values) => values.OfType<string>();
                                   }
                                   """;
        var test = CreateNet80Test(Source, FixedSource);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a Cast to a different type than the checked one is not collapsed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WhereTypeCheckWithDifferentCastTypeIsNotReportedAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(object[] values) => values.Where(value => value is string).Cast<object>();
                              }
                              """;
        var test = CreateNet80Test(Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies hot-path projects can opt into LINQ method diagnostics (PSH1100).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task HotPathLinqCallIsReportedWhenEnabledAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.{|PSH1100:Select|}(value => value + 1);
                              }
                              """;
        var test = CreateNet80Test(Source);
        EnableHotPathRule(test);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the hot-path rule stays silent without the opt-in editorconfig key.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task HotPathLinqCallIsNotReportedByDefaultAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public object M(int[] values) => values.Select(value => value + 1);
                              }
                              """;
        var test = CreateNet80Test(Source);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies string instance methods with LINQ operator names are not reported as hot-path LINQ calls.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StringContainsIsNotHotPathLinqAsync()
    {
        const string Source = """
                              using System.Linq;

                              public sealed class C
                              {
                                  public bool M()
                                  {
                                      string name = "abc";
                                      var doesContain = name.Contains("ab");
                                      return doesContain;
                                  }
                              }
                              """;
        var test = CreateNet80Test(Source);
        EnableHotPathRule(test);

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies declaration and recursive type patterns in either lambda syntax collapse.</summary>
    /// <param name="predicate">The predicate that supplies the cast type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value => value is string text")]
    [Arguments("(value) => value is string")]
    [Arguments("(value) => value is string { }")]
    public async Task TypePatternCastIsCollapsedAsync(string predicate)
    {
        var source = $$"""
            using System.Linq;
            class C { object M(object[] values) => values.Where({{predicate}}).{|PSH1102:Cast<string>|}(); }
            """;
        const string FixedSource = """
            using System.Linq;
            class C { object M(object[] values) => values.OfType<string>(); }
            """;
        await CreateNet80Test(source, FixedSource).RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies filters that are not direct type tests retain their original chain.</summary>
    /// <param name="predicate">The non-collapsible filter.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("value => value is not null")]
    [Arguments("value => value is { }")]
    [Arguments("value => { return value is string; }")]
    [Arguments("(value) => { return value is string; }")]
    [Arguments("IsString")]
    [Arguments("value => value != null")]
    public async Task NonTypePredicateIsNotCollapsedAsync(string predicate)
    {
        var source = $$"""
            using System.Linq;
            class C
            {
                object M(object[] values) => values.Where({{predicate}}).Cast<string>();
                static bool IsString(object value) => value is string;
            }
            """;
        await CreateNet80Test(source).RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies syntactically similar chains cannot bypass the Enumerable symbol checks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonEnumerableAndNonWhereChainsAreCleanAsync() =>
        CreateNet80Test("""
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq;
            class C : IEnumerable<object>
            {
                public C Where(Func<object, bool> predicate) => this;
                public C Where() => this;
                public C Where(Func<object, bool> predicate, int count) => this;
                public bool Any() => true;
                public C Cast<T>() => this;
                public C Cast<T, U>() => this;
                public IEnumerator<object> GetEnumerator() => throw new NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                void M(object[] values)
                {
                    _ = Where(value => true).Any();
                    _ = this.Where(value => true).Any();
                    _ = this.Where(value => value is string).Cast<string>();
                    _ = this.Where(value => value is string).Cast<string, object>();
                    _ = this.Where().Cast<string>();
                    _ = this.Where(value => true, 1).Any();
                    _ = values.Select(value => value).Any();
                    _ = values.Select(value => value).Cast<string>();
                    _ = values.Where(value => true).Any(value => true);
                    _ = values.Where(value => true).ToArray();
                }
            }
            class D : IEnumerable<object>
            {
                public IEnumerable<object> Where(Func<object, bool> predicate) => this;
                public IEnumerator<object> GetEnumerator() => throw new NotImplementedException();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                void M()
                {
                    _ = this.Where(value => true).Any();
                    _ = this.Where(value => value is string).Cast<string>();
                }
            }
            """).RunAsync(CancellationToken.None);

    /// <summary>Verifies every hot-path operator family and simple invocation form is screened.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HotPathOperatorFamiliesAreReportedAsync()
    {
        const string Source = """
            using System.Linq;
            class C
            {
                static int Count(int[] values) => values.Length;
                static T First<T>(T[] values) => values[0];

                void M(int[] values, System.Func<int> factory)
                {
                    _ = Count(values);
                    _ = First<int>(values);
                    _ = values.{|PSH1100:Where|}(value => true);
                    _ = values.{|PSH1100:Reverse|}();
                    _ = values.{|PSH1100:Skip|}(1);
                    _ = values.{|PSH1100:Distinct|}();
                    _ = values.{|PSH1100:ToList|}();
                    _ = values.GetLength(0);
                    _ = factory!();
                    _ = values?.Count();
                }
            }
            """;
        var test = CreateNet80Test(Source);
        EnableHotPathRule(test);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies every recognized operator name binds to an actual Enumerable extension.</summary>
    /// <param name="method">The operator name.</param>
    /// <param name="arguments">Arguments selecting a valid overload.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Aggregate", "(a, b) => a + b")]
    [Arguments("All", "value => value > 0")]
    [Arguments("Any", "")]
    [Arguments("Contains", "1")]
    [Arguments("Count", "")]
    [Arguments("Max", "")]
    [Arguments("Min", "")]
    [Arguments("Sum", "")]
    [Arguments("DefaultIfEmpty", "")]
    [Arguments("ElementAt", "0")]
    [Arguments("First", "")]
    [Arguments("FirstOrDefault", "")]
    [Arguments("Last", "")]
    [Arguments("LastOrDefault", "")]
    [Arguments("Single", "")]
    [Arguments("SingleOrDefault", "")]
    [Arguments("Cast<int>", "")]
    [Arguments("OfType<int>", "")]
    [Arguments("Select", "value => value")]
    [Arguments("SelectMany", "value => values")]
    [Arguments("Where", "value => true")]
    [Arguments("OrderBy", "value => value")]
    [Arguments("OrderByDescending", "value => value")]
    [Arguments("Reverse", "")]
    [Arguments("ThenBy", "value => value")]
    [Arguments("ThenByDescending", "value => value")]
    [Arguments("Skip", "1")]
    [Arguments("SkipWhile", "value => true")]
    [Arguments("Take", "1")]
    [Arguments("TakeWhile", "value => true")]
    [Arguments("Append", "1")]
    [Arguments("Concat", "values")]
    [Arguments("Distinct", "")]
    [Arguments("Except", "values")]
    [Arguments("Intersect", "values")]
    [Arguments("Prepend", "1")]
    [Arguments("Union", "values")]
    [Arguments("Zip", "values, (a, b) => a + b")]
    [Arguments("GroupBy", "value => value")]
    [Arguments("ToArray", "")]
    [Arguments("ToDictionary", "value => value")]
    [Arguments("ToHashSet", "")]
    [Arguments("ToList", "")]
    [Arguments("ToLookup", "value => value")]
    public async Task EnumerableOperatorIsReportedWhenEnabledAsync(string method, string arguments)
    {
        var test = CreateNet80Test($$"""
            using System.Linq;
            class C { object M(IOrderedEnumerable<int> values) => values.{|PSH1100:{{method}}|}({{arguments}}); }
            """);
        EnableHotPathRule(test);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies each predicate terminal can absorb the preceding filter.</summary>
    /// <param name="terminal">The predicate terminal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("First")]
    [Arguments("FirstOrDefault")]
    [Arguments("Last")]
    [Arguments("LastOrDefault")]
    [Arguments("Single")]
    [Arguments("SingleOrDefault")]
    public async Task PredicateTerminalAbsorbsWhereAsync(string terminal)
    {
        var test = CreateNet80Test(
            $$"""using System.Linq; class C { int M(int[] values) => values.Where(value => value > 0).{|PSH1101:{{terminal}}|}(); }""",
            $$"""using System.Linq; class C { int M(int[] values) => values.{{terminal}}(value => value > 0); }""");
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies unresolved operator symbols do not become hot-path diagnostics.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnresolvedLinqNamesAreCleanAsync()
    {
        var test = CreateNet80Test("class C { void M() { _ = Missing.Any(); _ = Missing.Where(x => x is string).Cast<string>(); } }");
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        EnableHotPathRule(test);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies near-miss operator names do not match either chain or hot-path rules.</summary>
    /// <param name="name">The method name resembling a recognized operator.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("AggregatX")]
    [Arguments("AlX")]
    [Arguments("AnX")]
    [Arguments("ContainX")]
    [Arguments("CounX")]
    [Arguments("MaX")]
    [Arguments("MiX")]
    [Arguments("SuX")]
    [Arguments("DefaultIfEmptX")]
    [Arguments("ElementAX")]
    [Arguments("FirsX")]
    [Arguments("FirstOrDefaulX")]
    [Arguments("LasX")]
    [Arguments("LastOrDefaulX")]
    [Arguments("SinglX")]
    [Arguments("SingleOrDefaulX")]
    [Arguments("AppenX")]
    [Arguments("ConcaX")]
    [Arguments("DistincX")]
    [Arguments("ExcepX")]
    [Arguments("IntersecX")]
    [Arguments("PrepenX")]
    [Arguments("UnioX")]
    [Arguments("ZiX")]
    [Arguments("Axe")]
    [Arguments("Moo")]
    [Arguments("XirstOrDefault")]
    [Arguments("Other")]
    public async Task OperatorNameNearMissIsCleanAsync(string name)
    {
        var test = CreateNet80Test($$"""
            class C
            {
                C Where(System.Func<int, bool> predicate) => this;
                int {{name}}() => 0;
                int M() => this.Where(value => true).{{name}}();
            }
            """);
        EnableHotPathRule(test);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies accepted truthy spellings and explicit opt-out values.</summary>
    /// <param name="value">The option value.</param>
    /// <param name="enabled">Whether the value enables reporting.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("TRUE", true)]
    [Arguments("1", true)]
    [Arguments("YeS", true)]
    [Arguments("false", false)]
    [Arguments("0", false)]
    [Arguments("no", false)]
    public async Task HotPathOptionControlsReportingAsync(string value, bool enabled)
    {
        var test = CreateNet80Test($"using System.Linq; class C {{ bool M(int[] values) => values.{(enabled ? "{|PSH1100:Any|}" : "Any")}(); }}");
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, $"root = true\n[*.cs]\ndotnet_diagnostic.PSH1100.severity = warning\nperformancesharp.avoid_linq_on_hot_path = {value}\n"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a .NET 8 verifier test.</summary>
    /// <param name="source">The source.</param>
    /// <param name="fixedSource">The optional fixed source.</param>
    /// <returns>The configured test.</returns>
    private static VerifyLinqUsage.Test CreateNet80Test(string source, string? fixedSource = null)
    {
        var test = new VerifyLinqUsage.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = source };

        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        return test;
    }

    /// <summary>Enables the opt-in hot-path LINQ rule (PSH1100) for a verifier test.</summary>
    /// <param name="test">The verifier test.</param>
    private static void EnableHotPathRule(VerifyLinqUsage.Test test)
    {
        const string Config = """
                              root = true

                              [*.cs]
                              dotnet_diagnostic.PSH1100.severity = warning
                              performancesharp.avoid_linq_on_hot_path = true
                              """;
        test.TestState.AnalyzerConfigFiles.Add((EditorConfigPath, Config));
        test.FixedState.AnalyzerConfigFiles.Add((EditorConfigPath, Config));
    }
}
