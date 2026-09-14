// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>Finishes the fill-then-trim pattern: size a buffer for the most entries possible, write what qualifies, then trim.</summary>
internal static class ArrayBuffers
{
    /// <summary>Returns a buffer trimmed to the entries written into it.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="buffer">The buffer, filled from index zero.</param>
    /// <param name="count">How many leading entries were written.</param>
    /// <returns>The buffer itself when it is full, an empty array when nothing was written, otherwise a copy of the written entries.</returns>
    internal static T[] RightSize<T>(T[] buffer, int count)
    {
        if (count == buffer.Length)
        {
            return buffer;
        }

        if (count == 0)
        {
            return [];
        }

        var result = new T[count];
        Array.Copy(buffer, result, count);
        return result;
    }
}
