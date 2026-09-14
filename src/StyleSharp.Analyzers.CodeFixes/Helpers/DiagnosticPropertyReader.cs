// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reads the values an analyzer stashes in a diagnostic's properties for its code fix.</summary>
internal static class DiagnosticPropertyReader
{
    /// <summary>Reads an integer property, written in the invariant culture.</summary>
    /// <param name="diagnostic">The diagnostic to read.</param>
    /// <param name="key">The property key.</param>
    /// <param name="value">The parsed value, or -1 when the property is absent or does not parse.</param>
    /// <returns><see langword="true"/> when the property is present and parses.</returns>
    internal static bool TryGetInt32(Diagnostic diagnostic, string key, out int value)
    {
        value = -1;
        return diagnostic.Properties.TryGetValue(key, out var text)
            && text is not null
            && int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
