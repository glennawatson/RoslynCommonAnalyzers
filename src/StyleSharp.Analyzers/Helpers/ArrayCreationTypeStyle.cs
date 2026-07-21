// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The array-creation element-type form SST2270 normalizes to.</summary>
internal enum ArrayCreationTypeStyle
{
    /// <summary>Always name the element type: <c>new T[] { ... }</c>.</summary>
    Explicit,

    /// <summary>Always infer the element type: <c>new[] { ... }</c>.</summary>
    Implicit,

    /// <summary>Infer the element type only when the elements make it obvious.</summary>
    ImplicitWhenObvious
}
