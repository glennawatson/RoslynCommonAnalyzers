// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyPreferConst = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1402PreferConstOverStaticReadonlyAnalyzer,
    PerformanceSharp.Analyzers.Psh1402PreferConstOverStaticReadonlyCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1402 (prefer const over static readonly) and its fix.</summary>
public class PreferConstOverStaticReadonlyAnalyzerUnitTest
{
    /// <summary>Verifies a private static readonly int with a literal value becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateStaticReadonlyIntBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private static readonly int {|PSH1402:MaxRetries|} = 3;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private const int MaxRetries = 3;
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an internal static readonly string becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InternalStaticReadonlyStringBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  internal static readonly string {|PSH1402:Prefix|} = "app";
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       internal const string Prefix = "app";
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a field initialized from another const becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstReferenceInitializerBecomesConstAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private const int Base = 10;
                                  private static readonly int {|PSH1402:Limit|} = Base * 2;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private const int Base = 10;
                                       private const int Limit = Base * 2;
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an enum-typed field with a constant member initializer becomes const.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumTypedConstantBecomesConstAsync()
    {
        const string Source = """
                              public enum Level
                              {
                                  None = 0,
                                  High = 2,
                              }

                              public class C
                              {
                                  private static readonly Level {|PSH1402:DefaultLevel|} = Level.High;
                              }
                              """;
        const string FixedSource = """
                                   public enum Level
                                   {
                                       None = 0,
                                       High = 2,
                                   }

                                   public class C
                                   {
                                       private const Level DefaultLevel = Level.High;
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies remaining modifiers such as new survive the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewModifierIsPreservedAsync()
    {
        const string Source = """
                              public class B
                              {
                                  internal const int Value = 1;
                              }

                              public class D : B
                              {
                                  private new static readonly int {|PSH1402:Value|} = 2;
                              }
                              """;
        const string FixedSource = """
                                   public class B
                                   {
                                       internal const int Value = 1;
                                   }

                                   public class D : B
                                   {
                                       private new const int Value = 2;
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All converts every reported field in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private static readonly int {|PSH1402:First|} = 1;
                                  private static readonly int {|PSH1402:Second|} = 2;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private const int First = 1;
                                       private const int Second = 2;
                                   }
                                   """;
        await VerifyPreferConst.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a public static readonly field is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicStaticReadonlyIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                public static readonly int MaxRetries = 3;
            }
            """);

    /// <summary>Verifies a non-constant initializer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewObjectInitializerIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static readonly object Gate = new object();
            }
            """);

    /// <summary>Verifies an instance readonly field is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonStaticReadonlyIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly int _count = 3;
            }
            """);

    /// <summary>Verifies a static readonly field of a type that cannot be const is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticReadonlyGuidIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static readonly System.Guid Id = new System.Guid("7f8b52e8-84a9-4f43-a58a-1f0ee9b56b4d");
            }
            """);

    /// <summary>Verifies a multi-variable declaration is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiVariableDeclarationIsCleanAsync()
        => await VerifyPreferConst.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static readonly int A = 1, B = 2;
            }
            """);
}
