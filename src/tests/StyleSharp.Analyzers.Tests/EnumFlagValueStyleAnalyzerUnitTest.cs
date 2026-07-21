// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEnumFlagValueStyle = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2272EnumFlagValueStyleAnalyzer,
    StyleSharp.Analyzers.Sst2272EnumFlagValueStyleCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for SST2272 (normalize Flags-enum single-bit value style). The rule is disabled by default, so
/// every test enables it through an <c>.editorconfig</c> severity entry and, where relevant, sets the option.
/// </summary>
public class EnumFlagValueStyleAnalyzerUnitTest
{
    /// <summary>Verifies decimal single-bit members become bit shifts under the default style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DecimalMembersBecomeShiftsUnderDefaultAsync()
    {
        const string Source = """
                              [System.Flags]
                              public enum E
                              {
                                  None = 0,
                                  A = {|SST2272:1|},
                                  B = {|SST2272:2|},
                                  C = {|SST2272:4|},
                                  All = 7,
                              }
                              """;
        const string FixedSource = """
                                   [System.Flags]
                                   public enum E
                                   {
                                       None = 0,
                                       A = 1 << 0,
                                       B = 1 << 1,
                                       C = 1 << 2,
                                       All = 7,
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: null);
    }

    /// <summary>Verifies bit-shift members become decimal literals when the style is <c>decimal</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShiftMembersBecomeDecimalsWhenConfiguredAsync()
    {
        const string Source = """
                              [System.Flags]
                              public enum E
                              {
                                  None = 0,
                                  A = {|SST2272:1 << 0|},
                                  B = {|SST2272:1 << 1|},
                                  C = {|SST2272:1 << 3|},
                              }
                              """;
        const string FixedSource = """
                                   [System.Flags]
                                   public enum E
                                   {
                                       None = 0,
                                       A = 1,
                                       B = 2,
                                       C = 8,
                                   }
                                   """;
        await RunAsync(Source, FixedSource, style: "decimal");
    }

    /// <summary>Verifies a shift member is left alone under the default shift style.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShiftMemberIsCleanUnderDefaultAsync()
    {
        const string Source = """
                              [System.Flags]
                              public enum E
                              {
                                  None = 0,
                                  A = 1 << 0,
                                  B = 1 << 1,
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Verifies a combined value and a zero member are never normalized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CombinedAndZeroMembersAreCleanAsync()
    {
        const string Source = """
                              [System.Flags]
                              public enum E
                              {
                                  None = 0,
                                  ReadWrite = 3,
                                  All = 15,
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Verifies an enum without the Flags attribute is never normalized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonFlagsEnumIsCleanAsync()
    {
        const string Source = """
                              public enum E
                              {
                                  A = 1,
                                  B = 2,
                                  C = 4,
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Verifies members with no explicit value are never normalized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitMembersAreCleanAsync()
    {
        const string Source = """
                              [System.Flags]
                              public enum E
                              {
                                  A,
                                  B,
                              }
                              """;
        await VerifyCleanAsync(Source, style: null);
    }

    /// <summary>Runs a code-fix verification with the disabled rule enabled and the given style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <param name="style">The <c>enum_flag_value_style</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, string fixedSource, string? style)
    {
        var test = CreateTest(source, style);
        test.FixedCode = fixedSource;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that expects no diagnostics.</summary>
    /// <param name="source">The source with no markup.</param>
    /// <param name="style">The <c>enum_flag_value_style</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source, string? style)
    {
        var test = CreateTest(source, style);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Creates a verifier test with SST2272 enabled and an optional style option.</summary>
    /// <param name="source">The markup source.</param>
    /// <param name="style">The <c>enum_flag_value_style</c> value, or <see langword="null"/> to leave it unset.</param>
    /// <returns>The configured test.</returns>
    private static VerifyEnumFlagValueStyle.Test CreateTest(string source, string? style)
    {
        var test = new VerifyEnumFlagValueStyle.Test
        {
            TestCode = source,
        };

        var config = "root = true\n\n[*.cs]\ndotnet_diagnostic.SST2272.severity = warning\n";
        if (style is not null)
        {
            config += $"stylesharp.enum_flag_value_style = {style}\n";
        }

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", config));
        return test;
    }
}
