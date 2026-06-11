// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;

namespace StyleSharp.Analyzers;

/// <summary>
/// Resolves the analysis category for a diagnostic id so the generated <c>[SuppressMessage]</c>
/// records a meaningful first argument. The map is built once from the descriptors declared in the
/// analyzer assembly, so every StyleSharp (<c>SST####</c>) id resolves to its real category; ids the
/// analyzer assembly does not know (for example external <c>CA</c>/<c>SA</c>/<c>IDE</c> rules) fall back
/// to a neutral default. The category is informational only — Roslyn matches a suppression on its id.
/// </summary>
internal static class SuppressionCategoryResolver
{
    /// <summary>The category used when an id is not declared by the analyzer assembly.</summary>
    private const string DefaultCategory = "Usage";

    /// <summary>The diagnostic-id-to-category map, built once from the analyzer assembly.</summary>
    private static readonly Dictionary<string, string> Categories = BuildCategoryMap();

    /// <summary>Returns the category for a diagnostic id, or a neutral default when it is unknown.</summary>
    /// <param name="ruleId">The diagnostic id being suppressed.</param>
    /// <returns>The rule's declared category, or <c>Usage</c> when it is not known.</returns>
    public static string Resolve(string ruleId)
        => Categories.TryGetValue(ruleId, out var category) ? category : DefaultCategory;

    /// <summary>Builds the id-to-category map from every <see cref="DiagnosticDescriptor"/> in the analyzer assembly.</summary>
    /// <returns>The id-to-category map.</returns>
    private static Dictionary<string, string> BuildCategoryMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var assembly = typeof(Sst1426PragmaWarningDisableAnalyzer).Assembly;
            foreach (var type in assembly.GetTypes())
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (field.GetValue(null) is DiagnosticDescriptor descriptor)
                    {
                        map[descriptor.Id] = descriptor.Category;
                    }
                }
            }
        }
        catch (ReflectionTypeLoadException)
        {
            // Best effort: unresolved ids fall back to the default category.
        }

        return map;
    }
}
