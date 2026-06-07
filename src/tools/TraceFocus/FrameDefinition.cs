// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace TraceFocus;

/// <summary>Single frame entry from the speedscope shared frame table.</summary>
public sealed class FrameDefinition
{
    /// <summary>Gets the frame name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
