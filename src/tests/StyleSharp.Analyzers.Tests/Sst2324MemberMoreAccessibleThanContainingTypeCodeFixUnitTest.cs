// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNarrowAccess = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2324MemberMoreAccessibleThanContainingTypeAnalyzer,
    StyleSharp.Analyzers.Sst2324MemberMoreAccessibleThanContainingTypeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST2324 code fix (narrow a member to its container's accessibility).</summary>
public class Sst2324MemberMoreAccessibleThanContainingTypeCodeFixUnitTest
{
    /// <summary>Verifies a public method in an internal type becomes internal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicMethodInInternalTypeBecomesInternalAsync()
    {
        const string Source = """
                              internal class Container
                              {
                                  {|SST2324:public|} void Method()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class Container
                                   {
                                       internal void Method()
                                       {
                                       }
                                   }
                                   """;
        await VerifyNarrowAccess.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a nested type keeps the modifiers that carry no accessibility.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherModifiersSurviveTheNarrowingAsync()
    {
        const string Source = """
                              internal class Container
                              {
                                  {|SST2324:public|} static class Nested
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class Container
                                   {
                                       internal static class Nested
                                       {
                                       }
                                   }
                                   """;
        await VerifyNarrowAccess.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the member's documentation stays above the narrowed modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DocumentationStaysAboveTheMemberAsync()
    {
        const string Source = """
                              internal class Container
                              {
                                  /// <summary>Does a thing.</summary>
                                  {|SST2324:public|} void Method()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class Container
                                   {
                                       /// <summary>Does a thing.</summary>
                                       internal void Method()
                                       {
                                       }
                                   }
                                   """;
        await VerifyNarrowAccess.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a member of a private nested type is narrowed to private.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberOfPrivateNestedTypeBecomesPrivateAsync()
    {
        const string Source = """
                              public class Host
                              {
                                  private sealed class Harness
                                  {
                                      {|SST2324:public|} void Method()
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class Host
                                   {
                                       private sealed class Harness
                                       {
                                           private void Method()
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifyNarrowAccess.VerifyCodeFixAsync(Source, FixedSource);
    }
}
