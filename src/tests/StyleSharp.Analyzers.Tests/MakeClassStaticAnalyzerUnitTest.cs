// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyMakeStatic = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.TypeDesignAnalyzer,
    StyleSharp.Analyzers.MakeClassStaticCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1432 (class with only static members) and its fix.</summary>
public class MakeClassStaticAnalyzerUnitTest
{
    /// <summary>Verifies an all-static class is reported and marked static.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllStaticClassMarkedStaticAsync()
    {
        const string Source = """
                              public class {|SST1432:Helpers|}
                              {
                                  public static int Add(int x, int y) => x + y;
                              }
                              """;
        const string FixedSource = """
                                   public static class Helpers
                                   {
                                       public static int Add(int x, int y) => x + y;
                                   }
                                   """;
        await VerifyMakeStatic.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All marks every all-static class static in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class {|SST1432:Helpers|}
                              {
                                  public static int Add(int x, int y) => x + y;
                              }

                              public class {|SST1432:Utilities|}
                              {
                                  public static int Sub(int x, int y) => x - y;
                              }
                              """;
        const string FixedSource = """
                                   public static class Helpers
                                   {
                                       public static int Add(int x, int y) => x + y;
                                   }

                                   public static class Utilities
                                   {
                                       public static int Sub(int x, int y) => x - y;
                                   }
                                   """;
        await VerifyMakeStatic.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a partial part is clean when a sibling part declares an instance member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialPartWithInstanceMemberInSiblingIsCleanAsync()
    {
        var test = new VerifyMakeStatic.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Thing.Helpers.cs", """
                                         internal partial class Thing
                                         {
                                             internal static int Zero() => 0;
                                         }
                                         """),
                    ("Thing.cs", """
                                 internal partial class Thing
                                 {
                                     internal int Value { get; set; }
                                 }
                                 """),
                },
            },
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a partial part is clean when a sibling part declares a base type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialPartWithBaseTypeInSiblingIsCleanAsync()
    {
        var test = new VerifyMakeStatic.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Thing.Helpers.cs", """
                                         internal partial class Thing
                                         {
                                             internal static int Zero() => 0;
                                         }
                                         """),
                    ("Thing.cs", """
                                 internal partial class Thing : System.IDisposable
                                 {
                                     public void Dispose() { }
                                 }
                                 """),
                },
            },
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an all-static partial type is reported once, on its first part, and marked static.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllStaticPartialTypeIsReportedOnFirstPartAsync()
    {
        var test = new VerifyMakeStatic.Test
        {
            TestState =
            {
                Sources =
                {
                    ("Thing.cs", """
                                 internal partial class {|SST1432:Thing|}
                                 {
                                     internal static int Value => 0;
                                 }
                                 """),
                    ("Thing.Helpers.cs", """
                                         internal partial class Thing
                                         {
                                             internal static int Zero() => 0;
                                         }
                                         """),
                },
            },
            FixedState =
            {
                Sources =
                {
                    ("Thing.cs", """
                                 internal static partial class Thing
                                 {
                                     internal static int Value => 0;
                                 }
                                 """),
                    ("Thing.Helpers.cs", """
                                         internal partial class Thing
                                         {
                                             internal static int Zero() => 0;
                                         }
                                         """),
                },
            },
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a class with an instance member and an already-static class are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InstanceMembersAndStaticClassesAreCleanAsync()
        => await VerifyMakeStatic.VerifyAnalyzerAsync(
            """
            public class HasInstanceMember
            {
                public static int Shared;

                public int Instance;
            }

            public static class AlreadyStatic
            {
                public static int Value;
            }
            """);
}
