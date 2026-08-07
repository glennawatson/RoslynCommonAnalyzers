// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNoPublic = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1416NoPublicOnInternalTypeAnalyzer,
    StyleSharp.Analyzers.Sst1416NoPublicOnInternalTypeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1416 (do not declare public members in a non-public type) and its fix.</summary>
public class NoPublicOnInternalTypeAnalyzerUnitTest
{
    /// <summary>Verifies a public member of an internal type is reported (SST1416) and demoted to internal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMemberOfInternalTypeDemotedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1416:public|} void M()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       internal void M()
                                       {
                                       }
                                   }
                                   """;
        await VerifyNoPublic.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an override is not reported, since the base member fixes its accessibility.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Demoting <c>public override bool Equals(object?)</c> to <c>internal</c> does not compile — the
    /// override inherits <c>public</c> from <see cref="object"/>, and narrowing it is CS0507. Members the
    /// containing type does not get to choose the accessibility of are outside this rule.
    /// </remarks>
    [Test]
    public async Task OverridesAreCleanAsync()
        => await VerifyNoPublic.VerifyAnalyzerAsync(
            """
            #nullable enable

            internal readonly struct ImmutableEquatableArray<T>
            {
                public override bool Equals(object? obj) => obj is ImmutableEquatableArray<T>;

                public override int GetHashCode() => 0;

                public override string ToString() => "x";

                {|SST1416:public|} int Count => 0;
            }
            """);

    /// <summary>Verifies a virtual or abstract member is not reported, since every override inherits from it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Demoting the base leaves the override stranded — the same CS0507, from the other end. The containing
    /// type cannot see who overrides it, so it cannot narrow the member on its own.
    /// </remarks>
    [Test]
    public async Task VirtualAndAbstractMembersAreCleanAsync()
        => await VerifyNoPublic.VerifyAnalyzerAsync(
            """
            internal abstract class Base
            {
                public abstract int Size { get; }

                public virtual void Work()
                {
                }

                {|SST1416:public|} void Plain()
                {
                }
            }

            internal sealed class Derived : Base
            {
                public override int Size => 1;

                public override void Work()
                {
                }
            }
            """);

    /// <summary>Verifies interface implementations and members of a public type are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceImplementationAndPublicTypeAreCleanAsync()
        => await VerifyNoPublic.VerifyAnalyzerAsync(
            """
            internal class C : System.IDisposable
            {
                public void Dispose()
                {
                }
            }

            public class D
            {
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies Fix All demotes every public member of an internal type (SST1416) in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1416:public|} void M()
                                  {
                                  }

                                  {|SST1416:public|} void N()
                                  {
                                  }

                                  {|SST1416:public|} void O()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       internal void M()
                                       {
                                       }

                                       internal void N()
                                       {
                                       }

                                       internal void O()
                                       {
                                       }
                                   }
                                   """;
        await VerifyNoPublic.VerifyCodeFixAsync(Source, FixedSource);
    }
}
