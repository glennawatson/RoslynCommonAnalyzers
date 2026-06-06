// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyReadonlyField = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.FieldShouldBeReadonlyAnalyzer,
    StyleSharp.Analyzers.FieldShouldBeReadonlyCodeFixProvider>;

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
}
