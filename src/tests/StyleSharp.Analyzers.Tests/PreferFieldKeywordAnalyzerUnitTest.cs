// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFieldKeyword = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.PreferFieldKeywordAnalyzer,
    StyleSharp.Analyzers.PreferFieldKeywordCodeFixProvider>;

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
