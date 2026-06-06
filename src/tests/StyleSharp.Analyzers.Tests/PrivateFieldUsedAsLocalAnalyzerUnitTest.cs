// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFieldLocal = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.PrivateFieldUsedAsLocalAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1422 (private field used only as local storage).</summary>
public class PrivateFieldUsedAsLocalAnalyzerUnitTest
{
    /// <summary>Verifies a field reset at the start of its only using method is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ResetTemporaryFieldIsReportedAsync()
        => await VerifyFieldLocal.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int {|SST1422:_total|};

                public int Sum(int value)
                {
                    _total = 0;
                    _total += value;
                    return _total;
                }
            }
            """);

    /// <summary>Verifies cross-method state is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FieldUsedByMultipleMethodsIsCleanAsync()
        => await VerifyFieldLocal.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _total;

                public void Add(int value) => _total += value;

                public int Read() => _total;
            }
            """);
}
