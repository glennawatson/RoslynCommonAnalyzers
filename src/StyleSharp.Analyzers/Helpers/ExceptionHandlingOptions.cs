// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1429 settings for one syntax tree.</summary>
/// <param name="CheckConstantReturningCatch">Whether a catch whose body only returns a constant counts as swallowing.</param>
internal readonly record struct ExceptionHandlingOptions(bool CheckConstantReturningCatch)
{
    /// <summary>The rule-specific constant-returning-catch key.</summary>
    private const string CheckConstantReturningCatchRuleKey = "stylesharp.SST1429.check_constant_returning_catch";

    /// <summary>The project-wide constant-returning-catch key.</summary>
    private const string CheckConstantReturningCatchGeneralKey = "stylesharp.check_constant_returning_catch";

    /// <summary>Reads the settings for one tree.</summary>
    /// <param name="options">The analyzer config options for the catch clause's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// The wider shape is off unless it is asked for. <c>catch { return default; }</c> is sometimes a
    /// deliberate "no value here" on a Try-style member rather than a swallowed failure, and only the
    /// codebase knows which it is — so the empty catch stays reported everywhere and the constant-returning
    /// one is opted into.
    /// </remarks>
    public static ExceptionHandlingOptions Read(AnalyzerConfigOptions options)
        => new(AnalyzerOptionReader.ReadBool(options, CheckConstantReturningCatchRuleKey, CheckConstantReturningCatchGeneralKey));
}
