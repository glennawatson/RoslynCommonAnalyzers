// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEnumMemberOrder = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1222EnumMemberOrderAnalyzer,
    StyleSharp.Analyzers.Sst1222EnumMemberOrderCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1222EnumMemberOrderAnalyzer"/> and its code fix (SST1222).</summary>
public class EnumMemberOrderAnalyzerUnitTest
{
    /// <summary>Verifies out-of-order members are reported and sorted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OutOfOrderMembersAreFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal enum Level
                              {
                                  Charlie = 3,
                                  {|SST1222:Alpha|} = 1,
                                  Bravo = 2,
                              }
                              """;
        const string FixedSource = """
                                   internal enum Level
                                   {
                                       Alpha = 1,
                                       Bravo = 2,
                                       Charlie = 3,
                                   }
                                   """;
        await VerifyEnumMemberOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a single swap at the end is reported and sorted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrailingSwapIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal enum Level
                              {
                                  Alpha = 1,
                                  Charlie = 3,
                                  {|SST1222:Bravo|} = 2,
                              }
                              """;
        const string FixedSource = """
                                   internal enum Level
                                   {
                                       Alpha = 1,
                                       Bravo = 2,
                                       Charlie = 3,
                                   }
                                   """;
        await VerifyEnumMemberOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies negative values sort ahead of positive ones.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegativeValueIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal enum Level
                              {
                                  Zero = 0,
                                  {|SST1222:Missing|} = -1,
                              }
                              """;
        const string FixedSource = """
                                   internal enum Level
                                   {
                                       Missing = -1,
                                       Zero = 0,
                                   }
                                   """;
        await VerifyEnumMemberOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies members already in ascending order are left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AscendingMembersAreCleanAsync()
        => await VerifyEnumMemberOrder.VerifyAnalyzerAsync(
            """
            internal enum Level
            {
                Alpha = 1,
                Bravo = 2,
                Charlie = 3,
            }
            """);

    /// <summary>Verifies a flags enum keeps its own grouping.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FlagsEnumIsCleanAsync()
        => await VerifyEnumMemberOrder.VerifyAnalyzerAsync(
            """
            using System;

            [Flags]
            internal enum Access
            {
                None = 0,
                Write = 2,
                Read = 1,
            }
            """);

    /// <summary>Verifies a fully qualified flags attribute is recognised.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedFlagsAttributeIsCleanAsync()
        => await VerifyEnumMemberOrder.VerifyAnalyzerAsync(
            """
            [System.Flags]
            internal enum Access
            {
                None = 0,
                Write = 2,
                Read = 1,
            }
            """);

    /// <summary>Verifies an implicitly numbered enum is left alone; reordering would renumber it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitlyNumberedEnumIsCleanAsync()
        => await VerifyEnumMemberOrder.VerifyAnalyzerAsync(
            """
            internal enum Level
            {
                Charlie,
                Alpha,
                Bravo,
            }
            """);

    /// <summary>Verifies an enum with one member is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleMemberEnumIsCleanAsync()
        => await VerifyEnumMemberOrder.VerifyAnalyzerAsync(
            """
            internal enum Level
            {
                Only = 1,
            }
            """);

    /// <summary>Verifies an unrelated attribute does not exempt the enum.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedAttributeStillReportsAsync()
    {
        const string Source = """
                              using System;

                              [Obsolete]
                              internal enum Level
                              {
                                  Bravo = 2,
                                  {|SST1222:Alpha|} = 1,
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   [Obsolete]
                                   internal enum Level
                                   {
                                       Alpha = 1,
                                       Bravo = 2,
                                   }
                                   """;
        await VerifyEnumMemberOrder.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a documented member is reported but not reordered; the fix would separate the text.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DocumentedMembersAreReportedWithoutAFixAsync()
    {
        const string Source = """
                              internal enum Level
                              {
                                  /// <summary>The higher one.</summary>
                                  Bravo = 2,

                                  /// <summary>The lower one.</summary>
                                  {|SST1222:Alpha|} = 1,
                              }
                              """;
        await VerifyEnumMemberOrder.VerifyCodeFixAsync(Source, Source);
    }
}
