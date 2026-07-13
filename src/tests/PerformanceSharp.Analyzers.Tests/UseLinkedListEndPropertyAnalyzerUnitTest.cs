// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1124UseLinkedListEndPropertyAnalyzer,
    PerformanceSharp.Analyzers.Psh1124UseLinkedListEndPropertyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1124UseLinkedListEndPropertyAnalyzer"/> (PSH1124 linked-list First and Last).</summary>
public class UseLinkedListEndPropertyAnalyzerUnitTest
{
    /// <summary>Verifies First on a linked list is flagged and rewritten to the node property's value.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LinkedListFirstIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(LinkedList<int> values) => values.{|PSH1124:First|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(LinkedList<int> values) => values.First.Value;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Last on a linked list is flagged and rewritten to the node property's value.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LinkedListLastIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public int M(LinkedList<int> values) => values.{|PSH1124:Last|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public int M(LinkedList<int> values) => values.Last.Value;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a reference-typed element still reads the node's value rather than the node.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceElementReadsTheNodeValueAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  public string M(LinkedList<string> values) => values.{|PSH1124:Last|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       public string M(LinkedList<string> values) => values.Last.Value;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a field receiver is flagged and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberReceiverIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;
                              using System.Linq;

                              public class C
                              {
                                  private readonly LinkedList<int> _values = new();

                                  public int Newest => _values.{|PSH1124:Last|}();
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;
                                   using System.Linq;

                                   public class C
                                   {
                                       private readonly LinkedList<int> _values = new();

                                       public int Newest => _values.Last.Value;
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies the predicate overload stays clean; it is asking a different question.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PredicateOverloadIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(LinkedList<int> values) => values.First(x => x > 0);
            }
            """);

    /// <summary>Verifies FirstOrDefault stays clean; the property cannot reproduce its empty-list answer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FirstOrDefaultIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(LinkedList<int> values) => values.FirstOrDefault();
            }
            """);

    /// <summary>Verifies LastOrDefault stays clean; the property cannot reproduce its empty-list answer.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LastOrDefaultIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(LinkedList<int> values) => values.LastOrDefault();
            }
            """);

    /// <summary>Verifies a list receiver stays clean; it has no end properties to read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ListReceiverIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(List<int> values) => values.Last();
            }
            """);

    /// <summary>Verifies a linked list exposed through an interface stays clean; the properties are not on the interface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceReceiverIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Collections.Generic;
            using System.Linq;

            public class C
            {
                public int M(ICollection<int> values) => values.Last();
            }
            """);

    /// <summary>Verifies a Queryable source stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QueryableSourceIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System.Linq;

            public class C
            {
                public int M(IQueryable<int> values) => values.Last();
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
