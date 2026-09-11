// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyLambdaSyntax = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1130UseLambdaSyntaxAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the use-lambda-syntax rule (SST1130).</summary>
public class UseLambdaSyntaxAnalyzerUnitTest
{
    /// <summary>Verifies an anonymous delegate is reported (SST1130).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AnonymousDelegateReportedAsync() =>
        VerifyLambdaSyntax.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                private static Action<int> M() => {|SST1130:delegate|} (int x) { _ = x; };
            }
            """);

    /// <summary>Verifies a lambda expression is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LambdaIsCleanAsync() =>
        VerifyLambdaSyntax.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                private static Action<int> M() => x => { _ = x; };
            }
            """);
}
