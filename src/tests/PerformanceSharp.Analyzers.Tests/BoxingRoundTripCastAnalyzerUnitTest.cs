// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1015BoxingRoundTripCastAnalyzer,
    PerformanceSharp.Analyzers.Psh1015BoxingRoundTripCastCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1015BoxingRoundTripCastAnalyzer"/> (PSH1015 boxing round-trip casts).</summary>
public class BoxingRoundTripCastAnalyzerUnitTest
{
    /// <summary>Verifies an enum-to-int round trip through object is flagged and cast directly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumRoundTripIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(DayOfWeek day) => {|PSH1015:(int)(object)day|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(DayOfWeek day) => (int)day;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an identity round trip is flagged and collapses to a direct cast.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdentityRoundTripIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(int value) => {|PSH1015:(int)(object)value|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public int M(int value) => (int)value;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the generic specialization pattern stays clean; the JIT elides that box.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterRoundTripIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public static int M<T>(T value) where T : struct
                    => typeof(T) == typeof(int) ? (int)(object)value : 0;
            }
            """);

    /// <summary>Verifies a cast from a reference type through object stays clean; nothing boxes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceTypeCastIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public string M(object value) => (string)(object)value;
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
