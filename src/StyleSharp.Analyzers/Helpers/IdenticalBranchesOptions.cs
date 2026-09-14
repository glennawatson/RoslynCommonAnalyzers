// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>The resolved SST1476 settings for one syntax tree.</summary>
/// <param name="MinimumStatements">The smallest branch body, in statements, that counts as a duplicate.</param>
internal readonly record struct IdenticalBranchesOptions(int MinimumStatements) : ITreeOptions<IdenticalBranchesOptions>
{
    /// <summary>The default smallest body size, which counts even a single statement.</summary>
    public const int DefaultMinimumStatements = 1;

    /// <summary>The rule-specific minimum-body key.</summary>
    private const string MinimumStatementsRuleKey = "stylesharp.SST1476.minimum_statements";

    /// <summary>The project-wide minimum-body key.</summary>
    private const string MinimumStatementsGeneralKey = "stylesharp.minimum_statements";

    /// <summary>Reads the settings for one tree, falling back to the default.</summary>
    /// <param name="options">The analyzer config options for the construct's tree.</param>
    /// <returns>The resolved settings.</returns>
    /// <remarks>
    /// An unset or unparsable value yields the default rather than an extreme, so a typo neither silences the
    /// rule nor changes which constructs it reports. A body of an expression — a conditional expression's arm,
    /// a switch expression's arm — counts as one statement, so raising the minimum above one excludes those
    /// shapes along with the one-line <c>if</c>.
    /// </remarks>
    internal static IdenticalBranchesOptions Read(AnalyzerConfigOptions options) =>
        new(AnalyzerOptionReader.ReadPositiveInt(options, MinimumStatementsRuleKey, MinimumStatementsGeneralKey, DefaultMinimumStatements));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IdenticalBranchesOptions ITreeOptions<IdenticalBranchesOptions>.ReadFrom(AnalyzerConfigOptions options) => Read(options);
}
