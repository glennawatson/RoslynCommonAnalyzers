// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRedundantDefault = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1403RemoveRedundantDefaultInitializationAnalyzer,
    PerformanceSharp.Analyzers.Psh1403RemoveRedundantDefaultInitializationCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1403 (remove redundant default initialization) and its fix.</summary>
public class RemoveRedundantDefaultInitializationAnalyzerUnitTest
{
    /// <summary>Verifies an int field initialized to zero has its initializer removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IntZeroInitializerRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _count {|PSH1403:= 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int _count;
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a bool field initialized to false has its initializer removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BoolFalseInitializerRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private bool _ready {|PSH1403:= false|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private bool _ready;
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a nullable string field initialized to null has its initializer removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableStringNullInitializerRemovedAsync()
    {
        const string Source = """
                              #nullable enable
                              public class C
                              {
                                  private string? _name {|PSH1403:= null|};
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable
                                   public class C
                                   {
                                       private string? _name;
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a double field initialized to positive zero has its initializer removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DoubleZeroInitializerRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private double _d {|PSH1403:= 0.0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private double _d;
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an enum field initialized to its zero member has its initializer removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumZeroMemberInitializerRemovedAsync()
    {
        const string Source = """
                              public enum State
                              {
                                  None = 0,
                                  Ready = 1,
                              }

                              public class C
                              {
                                  private State _state {|PSH1403:= State.None|};
                              }
                              """;
        const string FixedSource = """
                                   public enum State
                                   {
                                       None = 0,
                                       Ready = 1,
                                   }

                                   public class C
                                   {
                                       private State _state;
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a default literal initializer is removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultLiteralInitializerRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _count {|PSH1403:= default|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int _count;
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a default(T) initializer of the field type is removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultExpressionInitializerRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _count {|PSH1403:= default(int)|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int _count;
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a static field initialized to zero has its initializer removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticFieldZeroInitializerRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private static int _count {|PSH1403:= 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private static int _count;
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies only the defaulted declarator of a multi-variable declaration is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiVariableOnlyDefaultDeclaratorFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _a {|PSH1403:= 0|}, _b = 1;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int _a, _b = 1;
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes every redundant initializer in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _count {|PSH1403:= 0|};
                                  private bool _ready {|PSH1403:= false|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int _count;
                                       private bool _ready;
                                   }
                                   """;
        await VerifyRedundantDefault.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a null-forgiving initializer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullForgivingInitializerIsCleanAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            #nullable enable
            public class C
            {
                private string _name = null!;
            }
            """);

    /// <summary>Verifies an object-creation initializer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewObjectInitializerIsCleanAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public class C
            {
                private object _gate = new();
            }
            """);

    /// <summary>Verifies a non-default value is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonDefaultValueIsCleanAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _retries = 3;
            }
            """);

    /// <summary>Verifies a negative floating-point zero is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegativeZeroIsCleanAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public class C
            {
                private double _d = -0.0;
            }
            """);

    /// <summary>Verifies struct instance fields with initializers are skipped entirely.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StructFieldInitializerIsCleanAsync()
        => await VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public struct S
            {
                private int _count = 0;

                public S(int ignored)
                {
                    _ = ignored;
                }
            }
            """);
}
