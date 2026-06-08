// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyBool = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1143BooleanLiteralComparisonAnalyzer,
    StyleSharp.Analyzers.Sst1143BooleanLiteralComparisonCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1143 (do not compare to a boolean literal) and its fix.</summary>
public class BooleanLiteralComparisonAnalyzerUnitTest
{
    /// <summary>Verifies each comparison form is reported and simplified (negating where required).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComparisonsSimplifiedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M1(bool x) => {|SST1143:x == true|};

                                  public bool M2(bool x) => {|SST1143:x == false|};

                                  public bool M3(bool x) => {|SST1143:x != true|};

                                  public bool M4(bool x) => {|SST1143:x != false|};

                                  public bool M5(bool a, bool b) => {|SST1143:(a && b) == false|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M1(bool x) => x;

                                       public bool M2(bool x) => !x;

                                       public bool M3(bool x) => !x;

                                       public bool M4(bool x) => x;

                                       public bool M5(bool a, bool b) => !(a && b);
                                   }
                                   """;
        await VerifyBool.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies comparisons between two non-literal operands are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralComparisonIsCleanAsync()
        => await VerifyBool.VerifyAnalyzerAsync(
            """
            public class C
            {
                public bool M(bool x, bool y) => x == y;
            }
            """);

    /// <summary>Verifies comparing a nullable boolean to a literal is not reported (it is not redundant).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableBooleanComparisonIsCleanAsync()
        => await VerifyBool.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                private Task _task;

                public bool M1(bool? x) => x == true;

                public bool M2(bool? x) => x != false;

                public bool M3() => _task?.IsCompleted != false;

                public bool M4() => _task?.IsCompleted == true;
            }
            """);
}
