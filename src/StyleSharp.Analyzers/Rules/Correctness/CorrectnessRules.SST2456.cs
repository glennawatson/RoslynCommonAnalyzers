// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2456 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2456 — a field-like event that overrides or hides an inherited event gets a second backing field.</summary>
    public static readonly DiagnosticDescriptor RedeclaredFieldLikeEvent = Create(
        "SST2456",
        "A field-like event that overrides or hides an inherited event splits its subscriber list",
        "'{0}' is a field-like event that {1} an inherited event, so it gets a second backing field; handlers added through one type are invisible to raises through the other",
        RedeclaredFieldLikeEventDescription);

    /// <summary>The RedeclaredFieldLikeEvent rule description.</summary>
    private const string RedeclaredFieldLikeEventDescription =
        "A field-like event — one with no add/remove accessors — compiles to a hidden delegate field plus accessors that mutate "
        + "it. When such an event is declared 'override', or 'new' hiding an inherited event, the derived declaration brings a "
        + "hidden backing field of its own, so the base type and the derived type each keep a separate subscriber list. A handler "
        + "added through a reference of one type lands in one field; a raise that reads the other type's field walks the other, and "
        + "the handler silently never runs. Nothing throws; the notification just disappears. Give the event explicit add and "
        + "remove accessors so every type in the hierarchy shares one storage location, or drop the 'override'/'new' and reuse the "
        + "single inherited event.";
}
