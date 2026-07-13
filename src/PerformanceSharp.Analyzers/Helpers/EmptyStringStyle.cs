// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>The replacement PSH1204 steers an empty-string comparison towards.</summary>
/// <remarks>
/// The three are <b>not</b> interchangeable on <see langword="null"/>, which is why the choice is a
/// setting rather than a preference. <c>s == ""</c> answers <see langword="false"/> for a null string;
/// <see cref="Pattern"/> answers <see langword="false"/> too, <see cref="Length"/> throws, and
/// <see cref="IsNullOrEmpty"/> answers <see langword="true"/>. Only <see cref="Pattern"/> is an exact
/// equivalent for every input, so it is the default, and the other two are offered as a fix only where
/// the operand is known not to be null.
/// </remarks>
internal enum EmptyStringStyle
{
    /// <summary>The null-safe length pattern, <c>s is { Length: 0 }</c>. Requires C# 9.</summary>
    Pattern,

    /// <summary>The direct length test, <c>s.Length == 0</c>. Throws on a null string.</summary>
    Length,

    /// <summary>The framework helper, <c>string.IsNullOrEmpty(s)</c>. Answers true for a null string.</summary>
    IsNullOrEmpty
}
