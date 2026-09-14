// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests declaration names and configuration precedence for generated file names.</summary>
public class TypeFileNamingTests
{
    /// <summary>Checks generic and nongeneric declarations under both naming conventions.</summary>
    /// <param name="source">The declaration to name.</param>
    /// <param name="identifier">The unescaped identifier.</param>
    /// <param name="braces">The stem using type parameter names.</param>
    /// <param name="metadata">The stem using generic arity.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class @class {}", "class", "class", "class")]
    [Arguments("class Map<TKey, TValue> {}", "Map", "Map{TKey,TValue}", "Map`2")]
    [Arguments("delegate void Callback<T>();", "Callback", "Callback{T}", "Callback`1")]
    [Arguments("delegate void Callback();", "Callback", "Callback", "Callback")]
    [Arguments("enum Choice {}", "Choice", "Choice", "Choice")]
    [Arguments("namespace N {}", "", "", "")]
    public async Task StemsRespectDeclarationShapeAsync(string source, string identifier, string braces, string metadata)
    {
        var member = SyntaxFactory.ParseMemberDeclaration(source)!;
        await Assert.That(TypeFileNaming.Identifier(member)).IsEqualTo(identifier);
        await Assert.That(TypeFileNaming.Stem(member, useMetadataConvention: false)).IsEqualTo(braces);
        await Assert.That(TypeFileNaming.Stem(member, useMetadataConvention: true)).IsEqualTo(metadata);
    }

    /// <summary>Checks an explicitly empty type parameter list has no arity suffix.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyTypeParameterListUsesIdentifierAsync()
    {
        var member = SyntaxFactory.ClassDeclaration("C").WithTypeParameterList(SyntaxFactory.TypeParameterList());
        await Assert.That(TypeFileNaming.Stem(member, useMetadataConvention: false)).IsEqualTo("C");
        await Assert.That(TypeFileNaming.Stem(member, useMetadataConvention: true)).IsEqualTo("C");
    }

    /// <summary>Checks collection descends through namespaces without entering nested types.</summary>
    /// <param name="source">The compilation unit to inspect.</param>
    /// <param name="expected">The names in source order.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("namespace N { class C { class Nested {} } namespace Inner { delegate void D(); enum E {} } }", "C,D,E")]
    [Arguments("namespace N; record R; delegate void D<T>();", "R,D")]
    [Arguments("System.Console.ReadLine(); class C {}", "C")]
    [Arguments("", "")]
    public async Task TopLevelDeclarationsPreserveOrderAsync(string source, string expected)
    {
        var types = TypeFileNaming.TopLevelTypes(SyntaxFactory.ParseCompilationUnit(source));
        await Assert.That(string.Join(",", types.Select(TypeFileNaming.Identifier))).IsEqualTo(expected);
    }

    /// <summary>Checks rule overrides, general fallback, empty values, and case-insensitive metadata selection.</summary>
    /// <param name="rule">The rule-specific value, or null if absent.</param>
    /// <param name="general">The general value, or null if absent.</param>
    /// <param name="expected">Whether metadata naming is selected.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(null, null, false)]
    [Arguments(null, "", false)]
    [Arguments(null, "metadata", true)]
    [Arguments("", "metadata", true)]
    [Arguments("METADATA", "braces", true)]
    [Arguments("braces", "metadata", false)]
    [Arguments("unknown", null, false)]
    public async Task ConventionUsesRuleBeforeGeneralOptionAsync(string? rule, string? general, bool expected)
    {
        var options = new NamingOptions(rule, general);
        await Assert.That(TypeFileNaming.UseMetadataConvention(options, "SST1402")).IsEqualTo(expected);
    }

    /// <summary>Supplies independent rule and general naming options.</summary>
    /// <param name="rule">The rule-specific value.</param>
    /// <param name="general">The general value.</param>
    private sealed class NamingOptions(string? rule, string? general) : AnalyzerConfigOptions
    {
        /// <inheritdoc/>
        public override bool TryGetValue(string key, out string value)
        {
            var configured = key == "stylesharp.SST1402.file_naming_convention" ? rule : general;
            value = configured ?? string.Empty;
            return configured is not null;
        }
    }
}
