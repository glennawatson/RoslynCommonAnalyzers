// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyStaticGeneric = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.TypeDesignAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1431 (static member of a generic type ignoring its type parameters).</summary>
public class StaticMemberInGenericTypeAnalyzerUnitTest
{
    /// <summary>Verifies a static method on a generic type that ignores the type parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticMemberIgnoringTypeParameterReportedAsync()
        => await VerifyStaticGeneric.VerifyAnalyzerAsync(
            """
            public class Cache<T>
            {
                private int _hits;

                public static void {|SST1431:Clear|}()
                {
                }

                public static T Create() => default;
            }
            """);

    /// <summary>Verifies static members that use the type parameter and a private static helper are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterUsersAreCleanAsync()
        => await VerifyStaticGeneric.VerifyAnalyzerAsync(
            """
            public class Cache<T>
            {
                private int _hits;

                public static T Default { get; set; }

                public static bool Matches(T value) => value is not null;

                private static void Reset()
                {
                }
            }
            """);
}
