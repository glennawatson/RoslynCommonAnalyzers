// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds synthetic source for split date-and-time analysis (SST2017).</summary>
internal static class UseDateOnlyOrTimeOnlyBenchmarkSource
{
    /// <summary>Builds a compilation unit that reads a date or a time of day well or badly.</summary>
    /// <param name="types">The number of synthetic types to emit.</param>
    /// <param name="violating">Whether to emit rule violations.</param>
    /// <returns>The generated source text.</returns>
    public static string Generate(int types, bool violating)
        => $$"""
           using System;

           namespace Bench;

           {{BenchmarkSourceText.JoinBlocks(types, i => GenerateType(i, violating))}}
           """;

    /// <summary>Builds one clean or violating type.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <param name="violating">Whether to emit a violating type.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateType(int index, bool violating)
        => violating ? GenerateViolatingType(index) : GenerateCleanType(index);

    /// <summary>Builds one type that already says what it means.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    /// <remarks>
    /// Covers every rejection route the no-diagnostic path takes: a member read that is spelled neither
    /// <c>Date</c> nor <c>TimeOfDay</c>, which is what almost every member access in a real file is and costs
    /// one string comparison; a clock receiver, which SST2010 owns; and the same two members read from a
    /// <c>DateTimeOffset</c>, which is the case that actually pays for the bind before being rejected.
    /// </remarks>
    private static string GenerateCleanType(int index)
        => $$"""
           public sealed class C{{index}}
           {
               public DateOnly Day(DateTime value) => DateOnly.FromDateTime(value);

               public TimeOnly Time(DateTime value) => TimeOnly.FromDateTime(value);

               public int Year(DateTime value) => value.Year + {{index}};

               public DateTime ClockDay() => DateTime.UtcNow.Date;

               public DateTime OffsetDay(DateTimeOffset value) => value.Date;

               public TimeSpan OffsetTime(DateTimeOffset value) => value.TimeOfDay;
           }
           """;

    /// <summary>Builds one type that keeps a whole <c>DateTime</c> where it only ever meant half of one.</summary>
    /// <param name="index">The synthetic type index.</param>
    /// <returns>The generated type block.</returns>
    private static string GenerateViolatingType(int index)
        => $$"""
           public sealed class V{{index}}
           {
               public DateTime Day(DateTime value) => value.Date;

               public TimeSpan Time(DateTime value) => value.TimeOfDay;

               public DateTime Later(DateTime left, DateTime right) => left.Date > right.Date ? left.Date : right.Date;

               public int Index => {{index}};
           }
           """;
}
