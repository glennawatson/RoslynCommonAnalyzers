// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1451DateTimeKindAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1451DateTimeKindAnalyzer"/> (SST1451 DateTime without a kind).</summary>
public class Sst1451DateTimeKindAnalyzerUnitTest
{
    /// <summary>Verifies a kindless DateTime constructor is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task KindlessConstructorIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public DateTime M() => {|SST1451:new DateTime(2024, 1, 1)|};
            }
            """);

    /// <summary>Verifies a constructor that states the kind is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task KindConstructorIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public DateTime M() => new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }
            """);

    /// <summary>Verifies the parameterless constructor is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ParameterlessConstructorIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public DateTime M() => new DateTime();
            }
            """);

    /// <summary>Verifies a target-typed kindless creation is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TargetTypedKindlessCreationIsFlaggedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public DateTime M()
                {
                    DateTime value = {|SST1451:new(2024, 1, 1)|};
                    return value;
                }
            }
            """);

    /// <summary>Verifies other constructions are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OtherCreationsAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public DateTimeOffset M() => new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            }
            """);
}
