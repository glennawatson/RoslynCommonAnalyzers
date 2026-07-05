// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyReadonlyStructMember = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1460ReadonlyStructMemberAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1460ReadonlyStructMemberAnalyzer"/>.</summary>
public class ReadonlyStructMemberAnalyzerUnitTest
{
    /// <summary>Verifies a non-mutating struct method is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonMutatingMethodIsReportedAsync()
        => await VerifyReadonlyStructMember.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                private int _value;

                public int {|SST1460:Value|}() => _value;
            }
            """);

    /// <summary>Verifies a method with a call is skipped because mutation cannot be cheaply proven away.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MethodWithCallIsCleanAsync()
        => await VerifyReadonlyStructMember.VerifyAnalyzerAsync(
            """
            public struct Counter
            {
                public int Value() => GetValue();
                private readonly int GetValue() => 1;
            }
            """);
}
