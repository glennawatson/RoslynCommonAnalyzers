// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyPublicConstant = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2311PublicConstantFieldAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2311 (visible constants are compiled into their callers).</summary>
public class PublicConstantFieldAnalyzerUnitTest
{
    /// <summary>Verifies a public constant an outside assembly can read is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicConstantIsReportedAsync()
        => await VerifyPublicConstant.VerifyAnalyzerAsync(
            """
            public class Limits
            {
                public const int {|SST2311:Maximum|} = 100;

                public const string {|SST2311:Name|} = "limits";
            }
            """);

    /// <summary>Verifies each constant in a shared declaration is reported: they are baked in one by one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryDeclaratorIsReportedAsync()
        => await VerifyPublicConstant.VerifyAnalyzerAsync(
            """
            public class Limits
            {
                public const int {|SST2311:Minimum|} = 1, {|SST2311:Maximum|} = 100;
            }
            """);

    /// <summary>Verifies a constant is reported wherever an outside caller can still reach it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantInEveryVisibleContainerIsReportedAsync()
        => await VerifyPublicConstant.VerifyAnalyzerAsync(
            """
            public interface ILimits
            {
                const int {|SST2311:InterfaceDefault|} = 1;
            }

            public struct Point
            {
                public const int {|SST2311:Origin|} = 0;
            }

            public sealed class Sealed
            {
                public const int {|SST2311:Value|} = 1;
            }

            public class Outer
            {
                public class Nested
                {
                    public const int {|SST2311:Value|} = 1;
                }

                protected class ProtectedNested
                {
                    public const int {|SST2311:Value|} = 1;
                }
            }
            """);

    /// <summary>Verifies a protected constant is reported: a derived type in another assembly bakes it in too.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectedConstantIsReportedAsync()
        => await VerifyPublicConstant.VerifyAnalyzerAsync(
            """
            public class Limits
            {
                protected const int {|SST2311:Maximum|} = 100;

                protected internal const int {|SST2311:Minimum|} = 1;
            }
            """);

    /// <summary>Verifies a constant nobody outside the assembly can read is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Inside one assembly every caller is rebuilt together, so the stale-copy hazard cannot arise; the rule
    /// has nothing to say about these.
    /// </remarks>
    [Test]
    public async Task HiddenConstantIsCleanAsync()
        => await VerifyPublicConstant.VerifyAnalyzerAsync(
            """
            public class Limits
            {
                internal const int Internal = 1;

                private const int Private = 2;

                private protected const int PrivateProtected = 3;

                public static readonly int NotAConstant = 4;

                public int Read() => Internal + Private + PrivateProtected + NotAConstant;
            }

            internal class InternalLimits
            {
                public const int Value = 1;
            }

            public class Outer
            {
                private class PrivateNested
                {
                    public const int Value = 1;
                }

                public int Read() => PrivateNested.Value;
            }
            """);

    /// <summary>Verifies a protected constant in a sealed type is left alone: nothing can derive from it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ProtectedConstantInSealedTypeIsCleanAsync()
        => await VerifyPublicConstant.VerifyAnalyzerAsync(
            """
            public sealed class Limits
            {
                protected const int Maximum = 100;

                public int Read() => Maximum;
            }
            """);

    /// <summary>Verifies a local constant is left alone: it never leaves the method.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalConstantIsCleanAsync()
        => await VerifyPublicConstant.VerifyAnalyzerAsync(
            """
            public class Limits
            {
                public int Read()
                {
                    const int maximum = 100;
                    return maximum;
                }
            }
            """);

    /// <summary>Verifies an enum member is not a field declaration and is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumMemberIsCleanAsync()
        => await VerifyPublicConstant.VerifyAnalyzerAsync(
            """
            public enum Level
            {
                Low = 1,
                High = 2,
            }
            """);
}
