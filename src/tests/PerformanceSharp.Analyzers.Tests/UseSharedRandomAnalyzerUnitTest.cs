// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1412UseSharedRandomAnalyzer,
    PerformanceSharp.Analyzers.Psh1412UseSharedRandomCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1412UseSharedRandomAnalyzer"/> (PSH1412 use Random.Shared).</summary>
public class UseSharedRandomAnalyzerUnitTest
{
    /// <summary>Verifies a parameterless allocation in a local is reported and replaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterlessAllocationIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M()
                                  {
                                      var random = {|PSH1412:new Random()|};
                                      return random.Next();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M()
                                       {
                                           var random = Random.Shared;
                                           return random.Next();
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a cached field is still reported: the shared instance is thread-safe where a private one is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CachedFieldIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private static readonly Random Rng = {|PSH1412:new Random()|};

                                  public int M() => Rng.Next();
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private static readonly Random Rng = Random.Shared;

                                       public int M() => Rng.Next();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the target-typed <c>new()</c> form is reported and replaced with the simple name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TargetTypedAllocationIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private readonly Random _random = {|PSH1412:new()|};

                                  public int M() => _random.Next();
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private readonly Random _random = Random.Shared;

                                       public int M() => _random.Next();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a fully qualified allocation keeps the qualification the author wrote.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedAllocationKeepsQualificationAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M() => {|PSH1412:new System.Random()|}.Next();
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M() => System.Random.Shared.Next();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an allocation that is later reassigned still compiles after the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReassignedLocalIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(bool seeded)
                                  {
                                      var random = {|PSH1412:new Random()|};
                                      if (seeded)
                                      {
                                          random = new Random(42);
                                      }

                                      return random.Next();
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(bool seeded)
                                       {
                                           var random = Random.Shared;
                                           if (seeded)
                                           {
                                               random = new Random(42);
                                           }

                                           return random.Next();
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a seeded allocation is never reported: a seed asks for a reproducible sequence.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeededAllocationIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public class C
            {
                public int M() => new Random(42).Next();
            }
            """);

    /// <summary>Verifies a derived Random is never reported: its overrides would be traded away.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DerivedRandomIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using System;

            public sealed class StubRandom : Random
            {
                public override int Next() => 4;
            }

            public class C
            {
                public int M() => new StubRandom().Next();
            }
            """);

    /// <summary>Verifies another type's parameterless allocation is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherTypeAllocationIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public object M() => new object();
            }
            """);

    /// <summary>Verifies the rule is silent where Random.Shared does not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllocationIsCleanWithoutSharedApiAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M() => new Random().Next();
                              }
                              """;

        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source,
            FixedCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new Verify.Test
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
    private static async Task VerifyCleanAsync(string source) => await VerifyAsync(source, source);
}
