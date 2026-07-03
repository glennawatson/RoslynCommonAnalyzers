// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1114FreezeStaticLookupsAnalyzer,
    PerformanceSharp.Analyzers.Psh1114FreezeStaticLookupsCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1114FreezeStaticLookupsAnalyzer"/> (PSH1114 frozen lookups, opt-in).</summary>
public class FreezeStaticLookupsAnalyzerUnitTest
{
    /// <summary>The editorconfig that opts into the disabled-by-default rule.</summary>
    private const string OptInConfig = """
        root = true

        [*.cs]
        dotnet_diagnostic.PSH1114.severity = warning
        """;

    /// <summary>Verifies a read-only static dictionary is flagged and frozen by the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReadOnlyDictionaryIsFlaggedAndFrozenAsync()
    {
        const string Source = """
                              using System.Collections.Frozen;
                              using System.Collections.Generic;

                              public class C
                              {
                                  private static readonly Dictionary<string, int> {|PSH1114:Lookup|} = new Dictionary<string, int>
                                  {
                                      ["one"] = 1,
                                  };

                                  public int M(string key) => Lookup.TryGetValue(key, out var value) ? value : 0;
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Frozen;
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       private static readonly FrozenDictionary<string, int> Lookup = new Dictionary<string, int>
                                       {
                                           ["one"] = 1,
                                       }.ToFrozenDictionary();

                                       public int M(string key) => Lookup.TryGetValue(key, out var value) ? value : 0;
                                   }
                                   """;
        await VerifyOptInAsync(Source, FixedSource);
    }

    /// <summary>Verifies a comparer argument is carried through so lookup semantics never change.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparerIsCarriedThroughTheFreezeAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Frozen;
                              using System.Collections.Generic;

                              public class C
                              {
                                  private static readonly HashSet<string> {|PSH1114:Names|} = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                  {
                                      "one",
                                  };

                                  public bool M(string key) => Names.Contains(key);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Frozen;
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       private static readonly FrozenSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                       {
                                           "one",
                                       }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

                                       public bool M(string key) => Names.Contains(key);
                                   }
                                   """;
        await VerifyOptInAsync(Source, FixedSource);
    }

    /// <summary>Verifies a mutated dictionary stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MutatedDictionaryIsCleanAsync()
        => await VerifyOptInAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                private static readonly Dictionary<string, int> Lookup = new Dictionary<string, int>();

                public void M(string key) => Lookup.Add(key, 1);
            }
            """);

    /// <summary>Verifies a dictionary that escapes as an argument stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EscapingDictionaryIsCleanAsync()
        => await VerifyOptInAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                private static readonly Dictionary<string, int> Lookup = new Dictionary<string, int>();

                public void M() => Populate(Lookup);

                private static void Populate(Dictionary<string, int> target) => target["one"] = 1;
            }
            """);

    /// <summary>Verifies a non-private field stays clean; other assemblies' usage cannot be seen.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalFieldIsCleanAsync()
        => await VerifyOptInAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                internal static readonly Dictionary<string, int> Lookup = new Dictionary<string, int>();

                public int M(string key) => Lookup.TryGetValue(key, out var value) ? value : 0;
            }
            """);

    /// <summary>Verifies a partial type stays clean; another part could mutate the field.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialTypeIsCleanAsync()
        => await VerifyOptInAsync(
            """
            using System.Collections.Generic;

            public partial class C
            {
                private static readonly Dictionary<string, int> Lookup = new Dictionary<string, int>();

                public int M(string key) => Lookup.TryGetValue(key, out var value) ? value : 0;
            }
            """);

    /// <summary>Verifies the rule ships disabled by default; freezing only pays off for read-heavy tables.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleIsOffByDefaultAsync()
        => await Assert.That(CollectionRules.FreezeStaticLookups.IsEnabledByDefault).IsFalse();

    /// <summary>Runs an opted-in verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyOptInAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", OptInConfig));
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
            test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", OptInConfig));
        }

        await test.RunAsync(CancellationToken.None);
    }
}
