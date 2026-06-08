// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyReadonlyField = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1424FieldShouldBeReadonlyAnalyzer,
    StyleSharp.Analyzers.Sst1424FieldShouldBeReadonlyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1424 (make never-reassigned fields readonly).</summary>
public class FieldShouldBeReadonlyAnalyzerUnitTest
{
    /// <summary>Verifies a constructor-only assignment is reported and fixed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstructorOnlyAssignmentIsFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int {|SST1424:_value|};

                                  public C(int value) => _value = value;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private readonly int _value;

                                       public C(int value) => _value = value;
                                   }
                                   """;
        await VerifyReadonlyField.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a method assignment prevents the diagnostic.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MethodAssignmentIsCleanAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public void Set(int value) => _value = value;
            }
            """);

    /// <summary>Verifies several constructor-only fields in one type are each reported independently.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MultipleConstructorOnlyFieldsAreEachReportedAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int {|SST1424:_a|};
                private int {|SST1424:_b|};
                private int {|SST1424:_c|};

                public C(int value)
                {
                    _a = value;
                    _b = value;
                    _c = value;
                }
            }
            """);

    /// <summary>Verifies a same-named local written in another method does not block the report.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SameNameLocalWriteInOtherMethodDoesNotBlockReportAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int {|SST1424:_value|};

                public C(int value) => _value = value;

                public int Other()
                {
                    int _value = 3;
                    _value = 5;
                    return _value;
                }
            }
            """);

    /// <summary>Verifies a write inside a constructor lambda counts as outside the constructor and is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WriteInsideConstructorLambdaIsCleanAsync()
        => await VerifyReadonlyField.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                private int _value;

                public C()
                {
                    Action set = () => _value = 5;
                    set();
                }
            }
            """);
}
