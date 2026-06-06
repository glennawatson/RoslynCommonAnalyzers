// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyBasePrefix = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.DoNotPrefixWithBaseAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the redundant-base-prefix rule (SST1100).</summary>
public class DoNotPrefixWithBaseAnalyzerUnitTest
{
    /// <summary>Verifies a base call to a non-overridden member is reported (SST1100).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedundantBasePrefixReportedAsync()
        => await VerifyBasePrefix.VerifyAnalyzerAsync(
            """
            internal class Base
            {
                public virtual void Run()
                {
                }

                public void Help()
                {
                }
            }

            internal class Derived : Base
            {
                public override void Run()
                {
                }

                public void Call() => {|SST1100:base|}.Help();
            }
            """);

    /// <summary>Verifies a base call to an overridden member is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseCallToOverriddenMemberIsCleanAsync()
        => await VerifyBasePrefix.VerifyAnalyzerAsync(
            """
            internal class Base
            {
                public virtual void Run()
                {
                }
            }

            internal class Derived : Base
            {
                public override void Run() => base.Run();
            }
            """);
}
