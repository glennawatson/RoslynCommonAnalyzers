// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace TraceFocus;

/// <summary>Single open/close frame event inside an evented speedscope profile.</summary>
public sealed class FrameEvent
{
    /// <summary>Gets or sets the event type.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the frame index.</summary>
    [JsonPropertyName("frame")]
    public int Frame { get; set; }

    /// <summary>Gets or sets the event timestamp.</summary>
    [JsonPropertyName("at")]
    public double At { get; set; }

    /// <summary>Gets a value indicating whether this event opens a frame.</summary>
    public bool IsOpen => string.Equals(Type, "O", StringComparison.Ordinal);

    /// <summary>Gets a value indicating whether this event closes a frame.</summary>
    public bool IsClose => string.Equals(Type, "C", StringComparison.Ordinal);
}
