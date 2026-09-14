// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Decides whether the decoded content of a string literal is a database connection string that names a user but
/// supplies an empty or missing password (SES1203). Detection is purely syntactic: a cheap screen requires an '='
/// assignment and a ';' separator, then a single index-based scan splits the value into <c>key=value</c> segments
/// (on ';' and the first '=' of each segment, keys folded to ASCII lower-case) and accumulates which parts are
/// present -- a data-source key, a user key, a non-empty password, and integrated/trusted authentication. The
/// no-diagnostic path allocates nothing -- no <see cref="string.Split(char[])"/>, no substrings -- so an ordinary
/// literal is rejected after a couple of character searches. A connection string that carries a non-empty password,
/// uses integrated or trusted authentication, or names no user is not a match; those are either safe or another
/// rule's concern.
/// </summary>
internal static class EmptyConnectionStringPasswordClassifier
{
    /// <summary>The shortest literal that can carry both a data-source key and a user key (for example <c>Host=x;Uid=y</c>).</summary>
    private const int MinConnectionStringLength = 12;

    /// <summary>The <c>Integrated Security</c> key, folded to ASCII lower-case.</summary>
    private const string IntegratedSecurityKey = "integrated security";

    /// <summary>The <c>Trusted_Connection</c> key, folded to ASCII lower-case.</summary>
    private const string TrustedConnectionKey = "trusted_connection";

    /// <summary>The data-source keys, folded to ASCII lower-case, whose presence marks a connection string.</summary>
    private static readonly string[] DataSourceKeys = ["server", "data source", "host", "initial catalog", "database"];

    /// <summary>The user keys, folded to ASCII lower-case, whose presence marks user-name authentication.</summary>
    private static readonly string[] UserKeys = ["user id", "uid"];

    /// <summary>The password keys, folded to ASCII lower-case.</summary>
    private static readonly string[] PasswordKeys = ["password", "pwd"];

    /// <summary>The <c>Integrated Security</c> values, folded to ASCII lower-case, that enable integrated authentication.</summary>
    private static readonly string[] IntegratedSecurityTrueValues = ["true", "sspi"];

    /// <summary>The <c>Trusted_Connection</c> values, folded to ASCII lower-case, that enable trusted authentication.</summary>
    private static readonly string[] TrustedConnectionTrueValues = ["true", "yes"];

    /// <summary>The parts a connection-string segment can contribute to the blank-password decision.</summary>
    [Flags]
    private enum ConnectionStringParts
    {
        /// <summary>The segment contributes nothing relevant.</summary>
        None = 0,

        /// <summary>A data-source key is present.</summary>
        DataSource = 1 << 0,

        /// <summary>A user key is present.</summary>
        User = 1 << 1,

        /// <summary>A password key is present with a non-empty value.</summary>
        NonEmptyPassword = 1 << 2,

        /// <summary>Integrated or trusted authentication is enabled.</summary>
        IntegratedAuthentication = 1 << 3,
    }

    /// <summary>Returns whether a string literal's content is a connection string that names a user but has an empty or missing password.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns><see langword="true"/> when the content is a connection string with user-name authentication and a blank password.</returns>
    internal static bool IsEmptyPasswordConnectionString(string value)
    {
        // Cheap screen: a connection string needs at least an '=' assignment and a ';' separator between its keys.
        if (value.Length < MinConnectionStringLength || value.IndexOf('=') < 0 || value.IndexOf(';') < 0)
        {
            return false;
        }

        var parts = ConnectionStringParts.None;
        var length = value.Length;
        var segmentStart = 0;
        for (var i = 0; i <= length; i++)
        {
            if (i != length && value[i] != ';')
            {
                continue;
            }

            parts |= ClassifySegment(value, segmentStart, i);
            segmentStart = i + 1;
        }

        const ConnectionStringParts required = ConnectionStringParts.DataSource | ConnectionStringParts.User;
        if ((parts & required) != required || (parts & ConnectionStringParts.IntegratedAuthentication) != 0)
        {
            return false;
        }

        // A blank password is a connection string with no non-empty password value (empty key or no key at all).
        return (parts & ConnectionStringParts.NonEmptyPassword) == 0;
    }

    /// <summary>Classifies one <c>key=value</c> segment into the parts it contributes.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="segmentStart">The inclusive start of the segment.</param>
    /// <param name="segmentEnd">The exclusive end of the segment.</param>
    /// <returns>The parts the segment contributes.</returns>
    private static ConnectionStringParts ClassifySegment(string value, int segmentStart, int segmentEnd)
    {
        var equals = segmentEnd > segmentStart ? value.IndexOf('=', segmentStart, segmentEnd - segmentStart) : -1;
        if (equals < 0)
        {
            return ConnectionStringParts.None;
        }

        var keyStart = TrimStart(value, segmentStart, equals);
        var keyEnd = TrimEnd(value, keyStart, equals);

        if (AsciiText.RegionEqualsAnyLowercase(value, keyStart, keyEnd, DataSourceKeys))
        {
            return ConnectionStringParts.DataSource;
        }

        if (AsciiText.RegionEqualsAnyLowercase(value, keyStart, keyEnd, UserKeys))
        {
            return ConnectionStringParts.User;
        }

        var valueStart = TrimStart(value, equals + 1, segmentEnd);
        var valueEnd = TrimEnd(value, valueStart, segmentEnd);
        return ClassifyValueGatedSegment(value, keyStart, keyEnd, valueStart, valueEnd);
    }

    /// <summary>Classifies the segment keys whose contribution depends on the value (password and authentication).</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="keyStart">The inclusive start of the trimmed key.</param>
    /// <param name="keyEnd">The exclusive end of the trimmed key.</param>
    /// <param name="valueStart">The inclusive start of the trimmed value.</param>
    /// <param name="valueEnd">The exclusive end of the trimmed value.</param>
    /// <returns>The parts the segment contributes.</returns>
    private static ConnectionStringParts ClassifyValueGatedSegment(string value, int keyStart, int keyEnd, int valueStart, int valueEnd)
    {
        if (AsciiText.RegionEqualsAnyLowercase(value, keyStart, keyEnd, PasswordKeys))
        {
            return valueEnd > valueStart ? ConnectionStringParts.NonEmptyPassword : ConnectionStringParts.None;
        }

        if (AsciiText.RegionEqualsLowercase(value, keyStart, keyEnd, IntegratedSecurityKey))
        {
            return AsciiText.RegionEqualsAnyLowercase(value, valueStart, valueEnd, IntegratedSecurityTrueValues) ? ConnectionStringParts.IntegratedAuthentication : ConnectionStringParts.None;
        }

        if (AsciiText.RegionEqualsLowercase(value, keyStart, keyEnd, TrustedConnectionKey))
        {
            return AsciiText.RegionEqualsAnyLowercase(value, valueStart, valueEnd, TrustedConnectionTrueValues) ? ConnectionStringParts.IntegratedAuthentication : ConnectionStringParts.None;
        }

        return ConnectionStringParts.None;
    }

    /// <summary>Advances a start index past leading ASCII whitespace.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="start">The inclusive start of the span.</param>
    /// <param name="end">The exclusive end of the span.</param>
    /// <returns>The first non-whitespace index, or <paramref name="end"/> when the span is all whitespace.</returns>
    private static int TrimStart(string value, int start, int end)
    {
        while (start < end && IsWhitespace(value[start]))
        {
            start++;
        }

        return start;
    }

    /// <summary>Retreats an end index past trailing ASCII whitespace.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <param name="start">The inclusive start of the span.</param>
    /// <param name="end">The exclusive end of the span.</param>
    /// <returns>The exclusive end after trimming trailing whitespace.</returns>
    private static int TrimEnd(string value, int start, int end)
    {
        while (end > start && IsWhitespace(value[end - 1]))
        {
            end--;
        }

        return end;
    }

    /// <summary>Returns whether a character is one of the ASCII whitespace characters trimmed around keys and values.</summary>
    /// <param name="c">The character to test.</param>
    /// <returns><see langword="true"/> when the character is whitespace.</returns>
    private static bool IsWhitespace(char c) =>
        c is ' ' or '\t' or '\r' or '\n';
}
