// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCtorInitializer = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1128ConstructorInitializerOnOwnLineAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the constructor-initializer-on-own-line rule (SST1128).</summary>
public class ConstructorInitializerOnOwnLineAnalyzerUnitTest
{
    /// <summary>Verifies an initializer sharing the signature line is reported (SST1128).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializerSharingSignatureLineReportedAsync()
        => await VerifyCtorInitializer.VerifyAnalyzerAsync(
            """
            internal class Base
            {
                protected Base(int x)
                {
                    _ = x;
                }
            }

            internal class Derived : Base
            {
                public Derived() {|SST1128:: base(1)|}
                {
                }
            }
            """);

    /// <summary>Verifies an initializer on its own line is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitializerOnOwnLineIsCleanAsync()
        => await VerifyCtorInitializer.VerifyAnalyzerAsync(
            """
            internal class Base
            {
                protected Base(int x)
                {
                    _ = x;
                }
            }

            internal class Derived : Base
            {
                public Derived()
                    : base(1)
                {
                }
            }
            """);
}
