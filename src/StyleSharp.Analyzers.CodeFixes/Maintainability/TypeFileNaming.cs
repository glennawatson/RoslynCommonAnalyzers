// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Derives the file name that a type declaration should live in, mirroring the ReSharper/Rider
/// and the analyzer conventions. Generic types use a file-name-safe arity marker chosen by the
/// <c>stylesharp.file_naming_convention</c> option: <c>the analyzer</c> (default) renders the type
/// parameter names in braces (<c>Widget{TKey,TValue}.cs</c>) while <c>metadata</c> renders the
/// arity with a backtick (<c>Widget`2.cs</c>). Both forms are accepted by the SST1649 analyzer.
/// </summary>
internal static class TypeFileNaming
{
    /// <summary>The general option key selecting the generic file-naming convention.</summary>
    private const string ConventionKey = "stylesharp.file_naming_convention";

    /// <summary>The configuration value selecting the backtick-arity (metadata) convention.</summary>
    private const string MetadataValue = "metadata";

    /// <summary>Reads the configured generic file-naming convention for a rule, falling back to the general key, then the the analyzer default.</summary>
    /// <param name="options">The analyzer config options for the document's tree.</param>
    /// <param name="ruleId">The diagnostic id whose rule-specific override is checked first.</param>
    /// <returns><see langword="true"/> to use the backtick-arity (metadata) convention; otherwise the brace convention.</returns>
    public static bool UseMetadataConvention(AnalyzerConfigOptions options, string ruleId)
        => string.Equals(ResolveConvention(options, ruleId), MetadataValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds the file-name stem (without extension or suffix) for a type declaration.</summary>
    /// <param name="type">The type declaration to name.</param>
    /// <param name="useMetadataConvention"><see langword="true"/> for backtick arity; otherwise braces.</param>
    /// <returns>The conventional file-name stem.</returns>
    public static string Stem(MemberDeclarationSyntax type, bool useMetadataConvention)
    {
        var identifier = Identifier(type);
        var typeParameters = TypeParameters(type);
        if (typeParameters is null || typeParameters.Parameters.Count == 0)
        {
            return identifier;
        }

        if (useMetadataConvention)
        {
            return $"{identifier}`{typeParameters.Parameters.Count}";
        }

        var builder = new StringBuilder(identifier.Length + (typeParameters.Parameters.Count * 4) + 2);
        builder.Append(identifier).Append('{');
        for (var index = 0; index < typeParameters.Parameters.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(typeParameters.Parameters[index].Identifier.ValueText);
        }

        builder.Append('}');
        return builder.ToString();
    }

    /// <summary>Returns the declared identifier of a top-level type-like member, or an empty string.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The identifier text.</returns>
    public static string Identifier(MemberDeclarationSyntax member) => member switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
        _ => string.Empty
    };

    /// <summary>Collects every top-level type-like declaration (types and delegates) in the compilation unit.</summary>
    /// <param name="root">The compilation unit root.</param>
    /// <returns>The list of top-level type-like declarations in source order.</returns>
    public static List<MemberDeclarationSyntax> TopLevelTypes(CompilationUnitSyntax root)
    {
        var result = new List<MemberDeclarationSyntax>();
        CollectTopLevelTypes(root.Members, result);
        return result;
    }

    /// <summary>Resolves the configured convention value: rule-specific override, then general key, then empty.</summary>
    /// <param name="options">The analyzer config options for the document's tree.</param>
    /// <param name="ruleId">The diagnostic id whose rule-specific override is checked first.</param>
    /// <returns>The configured value, or an empty string when unset.</returns>
    private static string ResolveConvention(AnalyzerConfigOptions options, string ruleId)
    {
        if (options.TryGetValue($"stylesharp.{ruleId}.file_naming_convention", out var ruleValue) && ruleValue.Length > 0)
        {
            return ruleValue;
        }

        return options.TryGetValue(ConventionKey, out var value) && value.Length > 0 ? value : string.Empty;
    }

    /// <summary>Returns the type parameter list of a member, or <see langword="null"/> when it has none.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The type parameter list, or <see langword="null"/>.</returns>
    private static TypeParameterListSyntax? TypeParameters(MemberDeclarationSyntax member) => member switch
    {
        TypeDeclarationSyntax type => type.TypeParameterList,
        DelegateDeclarationSyntax @delegate => @delegate.TypeParameterList,
        _ => null
    };

    /// <summary>Walks members and namespaces, gathering top-level type-like declarations without descending into type bodies.</summary>
    /// <param name="members">The current member list.</param>
    /// <param name="result">The accumulator for type-like declarations.</param>
    private static void CollectTopLevelTypes(SyntaxList<MemberDeclarationSyntax> members, List<MemberDeclarationSyntax> result)
    {
        for (var index = 0; index < members.Count; index++)
        {
            switch (members[index])
            {
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    {
                        CollectTopLevelTypes(namespaceDeclaration.Members, result);
                        break;
                    }

                case BaseTypeDeclarationSyntax type:
                    {
                        result.Add(type);
                        break;
                    }

                case DelegateDeclarationSyntax @delegate:
                    {
                        result.Add(@delegate);
                        break;
                    }
            }
        }
    }
}
