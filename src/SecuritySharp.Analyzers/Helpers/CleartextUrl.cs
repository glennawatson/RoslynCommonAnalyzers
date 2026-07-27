// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Reads the authority out of a cleartext <c>http://</c> URL literal, and decides whether that authority
/// is a loopback address. Shared by the rules that report cleartext transport, so they agree on which
/// hosts are exempt — a rule that treated <c>127.0.0.1</c> as remote would report every local test.
/// </summary>
/// <remarks>
/// The parsing is deliberately textual rather than <see cref="Uri"/>-based: the input is a source literal
/// that may not be a well-formed URL, and constructing a <see cref="Uri"/> per literal would allocate on
/// an analyzer path that mostly finds nothing.
/// </remarks>
internal static class CleartextUrl
{
    /// <summary>The scheme prefix that marks a URL as cleartext.</summary>
    public const string HttpSchemePrefix = "http://";

    /// <summary>Reads the host out of a cleartext URL.</summary>
    /// <param name="text">The literal's text, beginning with <see cref="HttpSchemePrefix"/>.</param>
    /// <returns>The host segment; a bracketed IPv6 authority is returned without its brackets.</returns>
    public static string ExtractHost(string text)
    {
        var start = HttpSchemePrefix.Length;

        // A bracketed IPv6 authority (e.g. '[::1]') carries colons, so read the inner address to the closing bracket.
        if (text[start] == '[')
        {
            var inner = start + 1;
            var close = text.IndexOf(']', inner);
            return close < 0 ? text.Substring(inner) : text.Substring(inner, close - inner);
        }

        var end = start;
        while (end < text.Length)
        {
            var c = text[end];
            if (c is '/' or ':' or '?' or '#')
            {
                break;
            }

            end++;
        }

        return text.Substring(start, end - start);
    }

    /// <summary>Returns whether a parsed host is a loopback address that does not warrant a cleartext warning.</summary>
    /// <param name="host">The parsed host.</param>
    /// <returns><see langword="true"/> for a loopback or <c>*.localhost</c> host.</returns>
    public static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
            || string.Equals(host, "::1", StringComparison.Ordinal))
        {
            return true;
        }

        return host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }
}
