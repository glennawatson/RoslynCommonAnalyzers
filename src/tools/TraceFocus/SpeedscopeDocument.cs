// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace TraceFocus;

/// <summary>Root speedscope document model used by the trace-focus terminal tool.</summary>
public sealed class SpeedscopeDocument
{
    /// <summary>Gets or sets the shared frame table.</summary>
    [JsonPropertyName("shared")]
    public SharedSection Shared { get; set; } = new();

    /// <summary>Gets the exported profiles.</summary>
    [JsonInclude]
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonPropertyName("profiles")]
    public List<EventedProfile> Profiles { get; } = [];
}
