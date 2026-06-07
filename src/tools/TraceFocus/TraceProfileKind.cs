// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace TraceFocus;

/// <summary>Profile kinds that influence default trace filtering.</summary>
internal enum TraceProfileKind
{
    /// <summary>Infer the profile kind from the resolved trace path.</summary>
    Auto,

    /// <summary>CPU sampling trace defaults.</summary>
    Cpu,

    /// <summary>Allocation profiling trace defaults.</summary>
    Alloc
}
