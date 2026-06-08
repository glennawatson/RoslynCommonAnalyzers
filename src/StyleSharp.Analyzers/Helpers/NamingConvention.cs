// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A casing convention an identifier can be expected to follow.</summary>
internal enum NamingConvention
{
    /// <summary>PascalCase — begins with an upper-case letter.</summary>
    PascalCase,

    /// <summary>camelCase — begins with a lower-case letter.</summary>
    CamelCase
}
