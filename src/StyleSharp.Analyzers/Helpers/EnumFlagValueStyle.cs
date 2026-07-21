// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The Flags-enum single-bit value form SST2272 normalizes to.</summary>
internal enum EnumFlagValueStyle
{
    /// <summary><c>1 &lt;&lt; n</c> bit shifts.</summary>
    Shift,

    /// <summary>Decimal literals.</summary>
    Decimal
}
