// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyReadonlyMutableStructField = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1456ReadonlyMutableStructFieldAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1456ReadonlyMutableStructFieldAnalyzer"/>.</summary>
public class ReadonlyMutableStructFieldAnalyzerUnitTest
{
    /// <summary>Verifies a readonly field of a mutable source struct is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReadonlyMutableStructFieldIsReportedAsync() =>
        VerifyReadonlyMutableStructField.VerifyAnalyzerAsync(
            """
            public struct Mutable
            {
                public int Value;
            }

            public sealed class C
            {
                private readonly Mutable {|SST1456:_value|};
            }
            """);

    /// <summary>Verifies readonly structs and framework structs are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImmutableAndFrameworkStructsAreCleanAsync() =>
        VerifyReadonlyMutableStructField.VerifyAnalyzerAsync(
            """
            public readonly struct Immutable
            {
                private readonly int _value;
            }

            public sealed class C
            {
                private readonly Immutable _value;
                private readonly System.DateTime _created;
            }
            """);
}
