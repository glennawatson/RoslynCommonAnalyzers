// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The var-versus-explicit choice SST2271 normalizes to.</summary>
internal enum UseVarStyle
{
    /// <summary>Always use <c>var</c>.</summary>
    Always,

    /// <summary>Never use <c>var</c>; always name the type.</summary>
    Never,

    /// <summary>Use <c>var</c> only when the type is named on the right-hand side.</summary>
    WhenObvious
}
