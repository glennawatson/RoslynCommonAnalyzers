// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyRedundantCast = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExpressionSimplificationAnalyzer,
    StyleSharp.Analyzers.RedundantCastCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1175 (unnecessary casts) and its fix.</summary>
public class RedundantCastAnalyzerUnitTest
{
    /// <summary>Verifies a cast to the operand's own type is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdentityCastRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(int x) => ({|SST1175:int|})x;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(int x) => x;
                                   }
                                   """;
        await VerifyRedundantCast.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes every identity cast across a document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int A(int x) => ({|SST1175:int|})x;

                                  public int B(int y) => ({|SST1175:int|})y;

                                  public int D(int z) => ({|SST1175:int|})z;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int A(int x) => x;

                                       public int B(int y) => y;

                                       public int D(int z) => z;
                                   }
                                   """;
        await VerifyRedundantCast.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a cast that widens to a different type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WideningCastIsCleanAsync() =>
        VerifyRedundantCast.VerifyAnalyzerAsync(
            """
            public class C
            {
                public long M(int x) => (long)x;

                public object Boxed(int x) => (object)x;
            }
            """);

    /// <summary>Verifies a cast to a reference operand's own type is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceIdentityCastRemovedAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public string M(string x) => ({|SST1175:string|})x;
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public string M(string x) => x;
                                   }
                                   """;
        await VerifyRedundantCast.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a cast to the type a generic method already returns is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericReturnIdentityCastRemovedAsync()
    {
        const string Source = """
                              #nullable enable
                              public class Node
                              {
                              }

                              public class Unit : Node
                              {
                              }

                              public static class Ext
                              {
                                  public static TRoot Copy<TRoot>(this TRoot root)
                                      where TRoot : Node => root;
                              }

                              public class C
                              {
                                  public Unit M(Unit unit) => ({|SST1175:Unit|})unit.Copy();
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class Node
                                   {
                                   }

                                   public class Unit : Node
                                   {
                                   }

                                   public static class Ext
                                   {
                                       public static TRoot Copy<TRoot>(this TRoot root)
                                           where TRoot : Node => root;
                                   }

                                   public class C
                                   {
                                       public Unit M(Unit unit) => unit.Copy();
                                   }
                                   """;
        await VerifyRedundantCast.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>
    /// Verifies a cast asserting a maybe-null operand is not null is left in place. The compiler's own
    /// null-state diagnostics come with the shape, and are the reason the cast is worth writing.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullAssertingCastIsCleanAsync() =>
        VerifyRedundantCast.VerifyAnalyzerAsync(
            """
            #nullable enable
            public class C
            {
                public string M(string? maybeNull) => {|CS8603:{|CS8600:(string)maybeNull|}|};
            }
            """);

    /// <summary>Verifies a cast that keeps the operand's own nullable type is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AnnotatedIdentityCastRemovedAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  public string? M(string? maybeNull) => ({|SST1175:string?|})maybeNull;
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       public string? M(string? maybeNull) => maybeNull;
                                   }
                                   """;
        await VerifyRedundantCast.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a cast widening a not-null operand to the nullable type is left in place.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullWideningCastIsCleanAsync() =>
        VerifyRedundantCast.VerifyAnalyzerAsync(
            """
            #nullable enable
            public class C
            {
                public string? M(string notNull) => (string?)notNull;
            }
            """);

    /// <summary>Verifies a cast that changes only a type argument's nullability is left in place.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TypeArgumentNullabilityCastIsCleanAsync() =>
        VerifyRedundantCast.VerifyAnalyzerAsync(
            """
            #nullable enable
            using System.Collections.Generic;

            public class C
            {
                public IEnumerable<string?> M(IEnumerable<string> items) => (IEnumerable<string?>)items;
            }
            """);

    /// <summary>Verifies a downcast to a derived type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DowncastIsCleanAsync() =>
        VerifyRedundantCast.VerifyAnalyzerAsync(
            """
            #nullable enable
            public class Node
            {
            }

            public class Unit : Node
            {
            }

            public class C
            {
                public Unit M(Node node) => (Unit)node;
            }
            """);

    /// <summary>Verifies Fix All removes nested identity casts (an outer cast wrapping an inner cast) in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRemovesNestedCastsAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(int x) => ({|SST1175:int|})({|SST1175:int|})x;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(int x) => x;
                                   }
                                   """;
        await VerifyRedundantCast.VerifyCodeFixAsync(Source, FixedSource);
    }
}
