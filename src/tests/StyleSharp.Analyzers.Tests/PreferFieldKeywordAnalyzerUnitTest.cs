// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFieldKeyword = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2200PreferFieldKeywordAnalyzer,
    StyleSharp.Analyzers.Sst2200PreferFieldKeywordCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2200 (prefer the C# 14 field keyword).</summary>
public class PreferFieldKeywordAnalyzerUnitTest
{
    /// <summary>Verifies a single-use backing field with setter logic is replaced.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BackingFieldWithLogicIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _value;

                                  public int {|SST2200:Value|}
                                  {
                                      get => _value;
                                      set => _value = value < 0 ? 0 : value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Value
                                       {
                                           get => field;
                                           set => field = value < 0 ? 0 : value;
                                       }
                                   }
                                   """;
        await VerifyFieldKeyword.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an expression-bodied property is left to SST1420 rather than steered toward the field keyword.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExpressionBodiedPropertyIsCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public int Value => _value + 1;
            }
            """);

    /// <summary>Verifies a field used by another member is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SharedBackingFieldIsCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public int Value
                {
                    get => _value;
                    set => _value = value < 0 ? 0 : value;
                }

                public int Read() => _value;
            }
            """);

    /// <summary>Verifies a field referenced from a nested type is not reported, even though only its own property holds matching syntax.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BackingFieldReferencedInNestedTypeIsCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class Outer
            {
                private int _value;

                public int Value
                {
                    get => _value;
                    set => _value = value < 0 ? 0 : value;
                }

                private sealed class Nested
                {
                    private readonly Outer _outer;

                    public Nested(Outer outer) => _outer = outer;

                    public int Read() => _outer._value;
                }
            }
            """);

    /// <summary>Verifies a same-named local in another member does not block the report, because it binds to a different symbol.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SameNameLocalInOtherMethodIsReportedAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public int {|SST2200:Value|}
                {
                    get => _value;
                    set => _value = value < 0 ? 0 : value;
                }

                public int Other()
                {
                    var _value = 5;
                    return _value;
                }
            }
            """);

    /// <summary>Verifies expression-bodied trivial accessors are left to SST1420.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TrivialAccessorsAreCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public int Value
                {
                    get => _value;
                    set => _value = value;
                }
            }
            """);

    /// <summary>Verifies block-bodied trivial accessors are left to SST1420.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BlockBodiedTrivialAccessorsAreCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public int Value
                {
                    get { return _value; }
                    set { _value = value; }
                }
            }
            """);

    /// <summary>Verifies <c>this.</c>-qualified trivial accessors are left to SST1420.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ThisQualifiedTrivialAccessorsAreCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public int Value
                {
                    get => this._value;
                    set => this._value = value;
                }
            }
            """);

    /// <summary>Verifies a trivial get-only property is left to SST1420.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetOnlyTrivialAccessorIsCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly int _value;

                public int Value
                {
                    get => _value;
                }
            }
            """);

    /// <summary>Verifies a trivial write-only property has no accessor logic worth a field keyword.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>A write-only property has no getter, so the syntactic prepass cannot short-circuit it.</remarks>
    [Test]
    public async Task WriteOnlyTrivialAccessorIsCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public int Value
                {
                    set => _value = value;
                }
            }
            """);

    /// <summary>Verifies logic in the getter alone is enough to report.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetterWithLogicIsReportedAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public int {|SST2200:Value|}
                {
                    get => _value * 2;
                    set => _value = value;
                }
            }
            """);

    /// <summary>Verifies a <c>this.</c>-qualified backing field with setter logic is rewritten.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ThisQualifiedBackingFieldWithLogicIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _value;

                                  public int {|SST2200:Value|}
                                  {
                                      get => this._value;
                                      set => this._value = value < 0 ? 0 : value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int Value
                                       {
                                           get => field;
                                           set => field = value < 0 ? 0 : value;
                                       }
                                   }
                                   """;
        await VerifyFieldKeyword.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a static property is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StaticPropertyIsCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private static int _value;

                public static int Value
                {
                    get => _value;
                    set => _value = value < 0 ? 0 : value;
                }
            }
            """);

    /// <summary>Verifies an explicit interface implementation is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExplicitInterfaceImplementationIsCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public interface I
            {
                int Value { get; set; }
            }

            public class C : I
            {
                private int _value;

                int I.Value
                {
                    get => _value;
                    set => _value = value < 0 ? 0 : value;
                }
            }
            """);

    /// <summary>Verifies a volatile backing field is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task VolatileBackingFieldIsCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private volatile int _value;

                public int Value
                {
                    get => _value;
                    set => _value = value < 0 ? 0 : value;
                }
            }
            """);

    /// <summary>Verifies an attributed backing field is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AttributedBackingFieldIsCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            using System;

            [AttributeUsage(AttributeTargets.Field)]
            public sealed class MarkAttribute : Attribute
            {
            }

            public class C
            {
                [Mark]
                private int _value;

                public int Value
                {
                    get => _value;
                    set => _value = value < 0 ? 0 : value;
                }
            }
            """);

    /// <summary>Verifies a field declared alongside another declarator is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MultipleDeclaratorsBackingFieldIsCleanAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value, _other;

                public int Value
                {
                    get => _value;
                    set => _value = value < 0 ? 0 : value;
                }

                public int Other() => _other;
            }
            """);

    /// <summary>Verifies another type's field read before the backing field does not hide the report.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The backing-field search skips fields the containing type does not declare.</remarks>
    [Test]
    public async Task ForeignFieldReferencedFirstIsReportedAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public static class Other
            {
                public static int Y = 1;
            }

            public class C
            {
                private int _value;

                public int {|SST2200:Value|}
                {
                    get => Other.Y + _value;
                    set => _value = value < 0 ? 0 : value;
                }
            }
            """);

    /// <summary>Verifies every single-use backing field in one type is reported independently.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ManyBackingFieldsInOneTypeAreEachReportedAsync()
        => await VerifyFieldKeyword.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _a;
                private int _b;
                private int _c;

                public int {|SST2200:A|}
                {
                    get => _a;
                    set => _a = value < 0 ? 0 : value;
                }

                public int {|SST2200:B|}
                {
                    get => _b;
                    set => _b = value < 0 ? 0 : value;
                }

                public int {|SST2200:C2|}
                {
                    get => _c;
                    set => _c = value < 0 ? 0 : value;
                }
            }
            """);
}
