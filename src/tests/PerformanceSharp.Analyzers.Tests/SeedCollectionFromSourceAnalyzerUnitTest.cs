// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1112SeedCollectionFromSourceAnalyzer,
    PerformanceSharp.Analyzers.Psh1112SeedCollectionFromSourceCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1112SeedCollectionFromSourceAnalyzer"/> (PSH1112 constructor seeding).</summary>
public class SeedCollectionFromSourceAnalyzerUnitTest
{
    /// <summary>The editorconfig that turns the collection-expression preference off.</summary>
    private const string ConstructorPreferenceConfig = """
        root = true

        [*.cs]
        performancesharp.prefer_collection_expressions = false
        """;

    /// <summary>Verifies an explicitly typed list gains a spread collection expression by default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitlyTypedListBecomesCollectionExpressionAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public List<int> M(int[] source)
                                  {
                                      List<int> list = new List<int>();
                                      {|PSH1112:list.AddRange(source)|};
                                      return list;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public List<int> M(int[] source)
                                       {
                                           List<int> list = [.. source];
                                           return list;
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a var-typed list is seeded through the constructor because a spread has no target type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VarTypedListIsSeededThroughConstructorAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public List<int> M(int[] source)
                                  {
                                      var list = new List<int>();
                                      {|PSH1112:list.AddRange(source)|};
                                      return list;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public List<int> M(int[] source)
                                       {
                                           var list = new List<int>(source);
                                           return list;
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the editorconfig preference switches the fix to the constructor form.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PreferenceOffSeedsThroughConstructorAsync()
    {
        var test = new Verify.Test
        {
            TestCode = """
                       using System.Collections.Generic;

                       public class C
                       {
                           public List<int> M(int[] source)
                           {
                               List<int> list = new List<int>();
                               {|PSH1112:list.AddRange(source)|};
                               return list;
                           }
                       }
                       """,
            FixedCode = """
                        using System.Collections.Generic;

                        public class C
                        {
                            public List<int> M(int[] source)
                            {
                                List<int> list = new List<int>(source);
                                return list;
                            }
                        }
                        """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", ConstructorPreferenceConfig));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", ConstructorPreferenceConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a hash set union into a fresh instance is flagged and seeded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HashSetUnionWithIsFlaggedAndSeededAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public class C
                              {
                                  public HashSet<int> M(int[] source)
                                  {
                                      var set = new HashSet<int>();
                                      {|PSH1112:set.UnionWith(source)|};
                                      return set;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public class C
                                   {
                                       public HashSet<int> M(int[] source)
                                       {
                                           var set = new HashSet<int>(source);
                                           return set;
                                       }
                                   }
                                   """;
        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a creation that already passes a capacity stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapacitySeededCreationIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public List<int> M(int[] source)
                {
                    var list = new List<int>(source.Length);
                    list.AddRange(source);
                    return list;
                }
            }
            """);

    /// <summary>Verifies a creation with a collection initializer stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializedCreationIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public List<int> M(int[] source)
                {
                    var list = new List<int> { 1 };
                    list.AddRange(source);
                    return list;
                }
            }
            """);

    /// <summary>Verifies non-adjacent statements stay clean; something in between may matter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonAdjacentStatementsAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public List<int> M(int[] source)
                {
                    var list = new List<int>();
                    var length = source.Length;
                    list.AddRange(source);
                    return list;
                }
            }
            """);

    /// <summary>Verifies a source that mentions the receiver stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelfReferentialSourceIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public List<int> M(int[] source)
                {
                    var list = new List<int>();
                    list.AddRange(source.Where(value => value > list.Count));
                    return list;
                }
            }
            """);
}
