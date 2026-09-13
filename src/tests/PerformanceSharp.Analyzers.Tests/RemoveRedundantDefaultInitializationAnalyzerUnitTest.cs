// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyRedundantDefault = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1403RemoveRedundantDefaultInitializationAnalyzer,
    PerformanceSharp.Analyzers.Psh1403RemoveRedundantDefaultInitializationCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1403 (remove redundant default initialization) and its fix.</summary>
public class RemoveRedundantDefaultInitializationAnalyzerUnitTest
{
    /// <summary>Verifies the current rule retains character constants converted to numeric fields.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConvertedCharacterZeroIsCurrentlyRetainedAsync() =>
        VerifyRedundantDefault.VerifyAnalyzerAsync("""
            class C
            {
                public int Integral = '\0';
                public float Floating = '\0';
                public decimal Decimal = '\0';
            }
            """);

    /// <summary>Verifies typed zero constants and their nonzero neighbours are classified by value.</summary>
    /// <param name="type">The field's primitive type.</param>
    /// <param name="zero">A default constant.</param>
    /// <param name="other">A nondefault constant.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("sbyte", "(sbyte)0", "(sbyte)1")]
    [Arguments("byte", "(byte)0", "(byte)1")]
    [Arguments("short", "(short)0", "(short)1")]
    [Arguments("ushort", "(ushort)0", "(ushort)1")]
    [Arguments("uint", "0U", "1U")]
    [Arguments("long", "0L", "1L")]
    [Arguments("ulong", "0UL", "1UL")]
    [Arguments("char", "'\\0'", "'x'")]
    [Arguments("float", "0F", "1F")]
    [Arguments("float", "0", "-0F")]
    [Arguments("double", "0", "1D")]
    [Arguments("decimal", "0M", "1M")]
    [Arguments("decimal", "0", "1")]
    [Arguments("bool", "false", "true")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task TypedDefaultsAreDistinguishedAsync(string type, string zero, string other) =>
        VerifyRedundantDefault.VerifyAnalyzerAsync($$"""
            class C { public {{type}} Zero {|PSH1403:= {{zero}}|}, Other = {{other}}; }
            """);

    /// <summary>Verifies boxed constants, nullable values, and nonconstant initializers keep their meaning.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReferenceNullableAndCreationShapesAsync() =>
        VerifyRedundantDefault.VerifyAnalyzerAsync("""
            class C<T>
            {
                public object Boxed = 0;
                public object Converted = default(int);
                public int? Missing {|PSH1403:= null|};
                public int? Present = 0;
                public T Generic {|PSH1403:= default(T)|};
                public object Suppressed = ((null!));
                public object Created = new object();
                public int[] Array = new int[0];
                public int[] InferredArray = new[] { 1 };
                public int[] Collection = [];
                public int Computed = Next();
                public string Text = "";
                public int Uninitialized;
                static int Next() => 0;
            }
            """);

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullForgivingInitializerIsCleanAsync() =>
        VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            #nullable enable
            public class C
            {
                private string _name = null!;
            }
            """);

    /// <summary>Verifies an object-creation initializer is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NewObjectInitializerIsCleanAsync() =>
        VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public class C
            {
                private object _gate = new();
            }
            """);

    /// <summary>Verifies a non-default value is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonDefaultValueIsCleanAsync() =>
        VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _retries = 3;
            }
            """);

    /// <summary>Verifies a negative floating-point zero is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NegativeZeroIsCleanAsync() =>
        VerifyRedundantDefault.VerifyAnalyzerAsync(
            """
            public class C
            {
                private double _d = -0.0;
            }
            """);

    /// <summary>Verifies struct instance fields with initializers are skipped entirely.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StructFieldInitializerIsCleanAsync() =>
        VerifyRedundantDefault.VerifyAnalyzerAsync(
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
