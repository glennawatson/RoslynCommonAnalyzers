// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace TraceFocus;

/// <summary>Represents one ranked frame or stack entry in the printed report.</summary>
/// <param name="Name">The entry name.</param>
/// <param name="Value">The retained sampled value.</param>
internal sealed record RankedEntry(string Name, double Value);
