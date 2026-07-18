// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2475 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2475 — an entity's primary key is a temporal <c>DateTime</c>/<c>DateTimeOffset</c> value.</summary>
    public static readonly DiagnosticDescriptor TemporalPrimaryKey = Create(
        "SST2475",
        "An entity's primary key should not be a temporal type",
        "The primary key '{0}' is typed '{1}', a temporal value that collides within a tick and is not a stable identifier; use a surrogate key such as an integer or GUID",
        TemporalPrimaryKeyDescription);

    /// <summary>The TemporalPrimaryKey rule description.</summary>
    private const string TemporalPrimaryKeyDescription =
        "An entity whose primary key is a DateTime or DateTimeOffset stakes row identity on a wall-clock value, which is the "
        + "wrong thing to identify a row by. Two rows created in the same tick collide on the key; the value is not a stable "
        + "identifier because it shifts with the clock, the time zone, and daylight saving; a temporal clustered key orders and "
        + "fragments the table by insertion time rather than by identity; and the value round-trips imprecisely across providers, "
        + "which store DateTime and DateTimeOffset at different resolutions and offsets, so a key that matched on write can fail "
        + "to match on read. The key is identified either by an explicit Key attribute, or, on a type used as an entity through a "
        + "DbSet, by the primary-key naming convention that a public read-write property named Id or <TypeName>Id is the key. Use "
        + "a stable surrogate key — an integer identity or a GUID — and keep the timestamp as an ordinary column.";
}
