// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The configured documentation-coverage scope: which accessibilities the SST1600/SST1601/SST1602/SST1654
/// "must be documented" rules apply to. Mirrors the analyzer's <c>documentExposedElements</c> /
/// <c>documentInternalElements</c> / <c>documentPrivateElements</c> / <c>documentInterfaces</c> settings.
/// </summary>
/// <param name="ExposedElements">Whether public/protected elements require documentation.</param>
/// <param name="InternalElements">Whether internal elements require documentation.</param>
/// <param name="PrivateElements">Whether private elements (other than fields) require documentation.</param>
/// <param name="PrivateFields">Whether private fields require documentation. Gates fields only and is
/// independent of <paramref name="PrivateElements"/>; mirrors the analyzer's separate <c>documentPrivateFields</c> knob.</param>
/// <param name="Interfaces">How interfaces and their members are documented.</param>
internal readonly record struct DocumentationCoverage(
    bool ExposedElements,
    bool InternalElements,
    bool PrivateElements,
    bool PrivateFields,
    DocumentationInterfaceMode Interfaces);
