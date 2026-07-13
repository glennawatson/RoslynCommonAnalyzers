// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using VerifyEmptyCollection = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2306ReturnEmptyCollectionNotNullAnalyzer,
    StyleSharp.Analyzers.Sst2306ReturnEmptyCollectionNotNullCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2306 (return an empty collection instead of null) and its fix.</summary>
public class ReturnEmptyCollectionNotNullAnalyzerUnitTest
{
    /// <summary>Verifies an array-returning method's null becomes an empty collection expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayReturnIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int[] Find(bool found)
                                  {
                                      if (!found)
                                      {
                                          return {|SST2306:null|};
                                      }

                                      return new int[1];
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int[] Find(bool found)
                                       {
                                           if (!found)
                                           {
                                               return [];
                                           }

                                           return new int[1];
                                       }
                                   }
                                   """;
        await VerifyEmptyCollection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an expression-bodied member returning null is fixed with the empty array every framework has.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodyIsFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public IEnumerable<string> Names => {|SST2306:null|};

                                  public IReadOnlyList<int> Values() => {|SST2306:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public IEnumerable<string> Names => Array.Empty<string>();

                                       public IReadOnlyList<int> Values() => Array.Empty<int>();
                                   }
                                   """;
        await VerifyEmptyCollection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix names the empty array the way the file's imports require, not the way it reads best.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReplacementIsQualifiedWhenTheNamespaceIsNotImportedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public IEnumerable<int> Values() => {|SST2306:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public IEnumerable<int> Values() => System.Array.Empty<int>();
                                   }
                                   """;
        await VerifyEmptyCollection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a concrete collection type is constructed rather than emptied through an array.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcreteCollectionIsConstructedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public List<int> Items() => {|SST2306:null|};

                                  public Dictionary<string, int> Map() => {|SST2306:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public List<int> Items() => new List<int>();

                                       public Dictionary<string, int> Map() => new Dictionary<string, int>();
                                   }
                                   """;
        await VerifyEmptyCollection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a set or dictionary interface gets the concrete type an empty array cannot stand in for.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KeyedInterfacesGetTheirConcreteTypeAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public ISet<int> Tags() => {|SST2306:null|};

                                  public IDictionary<string, int> Map() => {|SST2306:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public ISet<int> Tags() => new HashSet<int>();

                                       public IDictionary<string, int> Map() => new Dictionary<string, int>();
                                   }
                                   """;
        await VerifyEmptyCollection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a null branch of a returned conditional is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalBranchIsFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  private readonly List<int> _items = new List<int>();

                                  public IReadOnlyList<int> Items(bool enabled) => enabled ? _items : {|SST2306:null|};
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       private readonly List<int> _items = new List<int>();

                                       public IReadOnlyList<int> Items(bool enabled) => enabled ? _items : Array.Empty<int>();
                                   }
                                   """;
        await VerifyEmptyCollection.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a property getter and an indexer getter are measured like a method.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GettersAreMeasuredAsync()
        => await VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public List<int> Items
                {
                    get { return {|SST2306:null|}; }
                }

                public int[] this[int index] => {|SST2306:null|};
            }
            """);

    /// <summary>Verifies a nullable return type is the author saying null is a value here.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableReturnTypeIsCleanAsync()
        => await VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            #nullable enable
            using System.Collections.Generic;

            public sealed class C
            {
                public IEnumerable<int>? Sequence() => null;

                public int[]? Values() => null;
            }
            """);

    /// <summary>Verifies a string is never treated as a collection of characters.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringReturnIsCleanAsync()
        => await VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string Name() => null;

                public string Text
                {
                    get { return null; }
                }
            }
            """);

    /// <summary>Verifies a task of a collection is left to the rules that own asynchronous returns.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TaskOfCollectionIsCleanAsync()
        => await VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public sealed class C
            {
                public Task<List<int>> ItemsAsync() => null;

                public async Task<List<int>> LoadAsync()
                {
                    await Task.Yield();
                    return null;
                }

                public ValueTask<int[]> ValuesAsync() => default;
            }
            """);

    /// <summary>Verifies a lambda takes its shape from its delegate, and a scalar is not a collection.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LambdasAndScalarsAreCleanAsync()
        => await VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class C
            {
                public object Scalar() => null;

                public Func<List<int>> Factory()
                {
                    return () => null;
                }
            }
            """);

    /// <summary>Verifies a member with no body has nothing to return.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractAndInterfaceMembersAreCleanAsync()
        => await VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public interface IStore
            {
                IReadOnlyList<int> Items();
            }

            public abstract class Base
            {
                public abstract int[] Values();
            }
            """);

    /// <summary>Verifies a local function is measured, and a non-empty return is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFunctionIsMeasuredAsync()
        => await VerifyEmptyCollection.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int[] Run()
                {
                    int[] Inner()
                    {
                        return {|SST2306:null|};
                    }

                    return Inner();
                }
            }
            """);

    /// <summary>Verifies the shared empty array is written where the language has no collection expressions.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BelowCSharp12TheEmptyArrayIsWrittenAsync()
    {
        const string Source = """
                              using System;
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public int[] Values()
                                  {
                                      return {|SST2306:null|};
                                  }

                                  public IEnumerable<string> Names()
                                  {
                                      return {|SST2306:null|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public int[] Values()
                                       {
                                           return Array.Empty<int>();
                                       }

                                       public IEnumerable<string> Names()
                                       {
                                           return Array.Empty<string>();
                                       }
                                   }
                                   """;
        var test = new VerifyEmptyCollection.Test
        {
            TestCode = Source,
            FixedCode = FixedSource,
        };

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp11));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
