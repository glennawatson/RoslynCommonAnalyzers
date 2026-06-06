// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace TraceFocus;

/// <summary>Evented profile exported by the speedscope format.</summary>
public sealed class EventedProfile
{
    /// <summary>Gets or sets the profile name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the speedscope profile type.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the profile unit.</summary>
    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    /// <summary>Gets or sets the profile start value.</summary>
    [JsonPropertyName("startValue")]
    public double StartValue { get; set; }

    /// <summary>Gets or sets the profile end value.</summary>
    [JsonPropertyName("endValue")]
    public double EndValue { get; set; }

    /// <summary>Gets the frame events.</summary>
    [JsonInclude]
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonPropertyName("events")]
    public List<FrameEvent> Events { get; } = [];

    /// <summary>Gets a value indicating whether this profile is evented.</summary>
    public bool IsEvented => string.Equals(Type, "evented", StringComparison.Ordinal);
}
