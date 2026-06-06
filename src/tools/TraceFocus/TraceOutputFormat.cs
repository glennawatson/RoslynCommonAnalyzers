// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace TraceFocus;

/// <summary>Output formats supported by the trace-focus terminal tool.</summary>
internal enum TraceOutputFormat
{
    /// <summary>Human-readable terminal summary.</summary>
    Text,

    /// <summary>Machine-readable JSON summary.</summary>
    Json,
}
