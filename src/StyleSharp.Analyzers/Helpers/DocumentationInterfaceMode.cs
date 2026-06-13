// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// How the documentation-coverage rules treat interfaces and their members, via the
/// <c>documentInterfaces</c> setting.
/// </summary>
internal enum DocumentationInterfaceMode
{
    /// <summary>Document every interface and interface member regardless of accessibility.</summary>
    All,

    /// <summary>Document only externally visible (non-internal) interfaces and their public members.</summary>
    Exposed,

    /// <summary>Never require documentation on interfaces or their members.</summary>
    None,
}
