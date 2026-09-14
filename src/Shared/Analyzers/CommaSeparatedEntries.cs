// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Enumerates the trimmed, non-empty entries of a comma-separated option value without allocating.</summary>
/// <remarks>
/// Each entry is a view of the option text, so a caller that keeps one as a string can reuse the text itself when the
/// entry spans all of it.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "SST2324", Justification = "TODO: Remove once SST2324 exempts the public members the foreach pattern binds to.")]
internal ref struct CommaSeparatedEntries
{
    /// <summary>The option text being split.</summary>
    private readonly string _value;

    /// <summary>The index the search for the next entry starts from.</summary>
    private int _start;

    /// <summary>Initializes a new instance of the <see cref="CommaSeparatedEntries"/> struct.</summary>
    /// <param name="value">The option text to split.</param>
    internal CommaSeparatedEntries(string value)
    {
        _value = value;
        _start = 0;
        Current = default;
    }

    /// <summary>Gets the current trimmed entry.</summary>
    public ReadOnlySpan<char> Current { get; private set; }

    /// <summary>Returns this walk as its own enumerator.</summary>
    /// <returns>The enumerator.</returns>
    public readonly CommaSeparatedEntries GetEnumerator() => this;

    /// <summary>Advances to the next non-empty entry.</summary>
    /// <returns><see langword="true"/> when an entry was found.</returns>
    public bool MoveNext()
    {
        while (_start < _value.Length)
        {
            var end = _value.IndexOf(',', _start);
            if (end < 0)
            {
                end = _value.Length;
            }

            var entry = AnalyzerOptionReader.TrimSegment(_value, _start, end);
            _start = end + 1;
            if (entry.IsEmpty)
            {
                continue;
            }

            Current = entry;
            return true;
        }

        return false;
    }

    /// <summary>Returns the most entries a value can hold, which sizes a buffer for them.</summary>
    /// <param name="value">The option text.</param>
    /// <returns>One more than the number of commas.</returns>
    internal static int MaxCount(string value)
    {
        var count = 1;
        foreach (var character in value)
        {
            if (character == ',')
            {
                count++;
            }
        }

        return count;
    }
}
