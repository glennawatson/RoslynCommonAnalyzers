// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>What a message-template placeholder resolves to once its name is read.</summary>
internal enum LogPlaceholderKind
{
    /// <summary>A placeholder whose name is a property identifier the value is captured under.</summary>
    Named,

    /// <summary>A placeholder whose name is all digits, so it is positional rather than a captured property.</summary>
    Numeric,

    /// <summary>A placeholder that is empty, whitespace, or whose name contains a character a name cannot.</summary>
    Malformed,
}
