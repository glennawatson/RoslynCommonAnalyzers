// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyExplicitCollection = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2101ExplicitCollectionExpressionAnalyzer,
    StyleSharp.Analyzers.CollectionExpressionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2101 (use an explicit collection expression).</summary>
public class ExplicitCollectionExpressionAnalyzerUnitTest
{
    /// <summary>Verifies an explicit array initializer is replaced with a collection expression.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitArrayIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int[] Values = {|SST2101:new[] { 1, 2, 3 }|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int[] Values = [ 1, 2, 3 ];
                                   }
                                   """;
        var test = new VerifyExplicitCollection.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
            FixedCode = FixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an argument a generic call infers its type argument from is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// A collection expression takes its type from the target, so replacing the creation leaves inference
    /// with nothing to work from and the call stops compiling with CS0411 or CS0311.
    /// </remarks>
    [Test]
    public async Task ArgumentSupplyingAnInferredTypeArgumentIsCleanAsync()
        => await VerifyExplicitCollection.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public static class Infer
            {
                public static void Single<T>(T value)
                {
                }

                public static void Sequence<T>(IEnumerable<T> values)
                {
                }

                public static void Pair<T>(T first, T second)
                {
                }
            }

            public class C
            {
                public void M()
                {
                    Infer.Single(new byte[] { 1, 2, 3 });
                    Infer.Sequence(new byte[] { 1, 2, 3 });
                    Infer.Pair(new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 });
                }
            }
            """);

    /// <summary>Verifies an argument whose parameter type is concrete is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ArgumentWithAConcreteParameterTypeIsStillReportedAsync()
        => await VerifyExplicitCollection.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public static class Concrete
            {
                public static void Take(byte[] values)
                {
                }

                public static void TakeList(List<int> values)
                {
                }
            }

            public class C
            {
                public void M()
                {
                    Concrete.Take({|SST2101:new byte[] { 1, 2, 3 }|});
                    Concrete.TakeList({|SST2101:new List<int> { 1, 2, 3 }|});
                }
            }
            """);

    /// <summary>Verifies a type parameter the receiver already fixed does not block the report.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TypeParameterFixedByTheReceiverIsStillReportedAsync()
        => await VerifyExplicitCollection.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M(List<byte[]> sink)
                {
                    sink.Add({|SST2101:new byte[] { 1, 2, 3 }|});
                }
            }
            """);

    /// <summary>Verifies dictionary-style and var initializers are not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AmbiguousInitializersAreCleanAsync()
        => await VerifyExplicitCollection.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public class C
            {
                public void M()
                {
                    var values = new[] { 1, 2, 3 };
                    Dictionary<int, string> map = new Dictionary<int, string> { { 1, "one" } };
                }
            }
            """);
}
