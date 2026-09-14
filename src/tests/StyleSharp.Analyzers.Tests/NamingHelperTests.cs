// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests identifier probes and naming suggestions independently of symbol eligibility.</summary>
public class NamingHelperTests
{
    /// <summary>Checks prefix probes handle empty identifiers and distinguish letter case.</summary>
    /// <param name="name">The identifier text.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("IValue")]
    [Arguments("TValue")]
    [Arguments("item")]
    [Arguments("_value")]
    [Arguments("1value")]
    public async Task PrefixProbesInspectFirstCharacterAsync(string name)
    {
        await Assert.That(NamingHelper.BeginsWithCapitalI(name)).IsEqualTo(name == "IValue");
        await Assert.That(NamingHelper.BeginsWithCapitalT(name)).IsEqualTo(name == "TValue");
        await Assert.That(NamingHelper.BeginsWithUpperCase(name)).IsEqualTo(name is "IValue" or "TValue");
        await Assert.That(NamingHelper.BeginsWithLowerCase(name)).IsEqualTo(name == "item");
        await Assert.That(NamingHelper.BeginsWithUnderscore(name)).IsEqualTo(name == "_value");
    }

    /// <summary>Checks underscore-only names and runtime field names use distinct contracts.</summary>
    /// <param name="name">The identifier text.</param>
    /// <param name="allUnderscores">Whether every character is an underscore.</param>
    /// <param name="camelField">Whether the name has one underscore and a lowercase initial.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", false, false)]
    [Arguments("_", true, false)]
    [Arguments("___", true, false)]
    [Arguments("value", false, false)]
    [Arguments("_value", false, true)]
    [Arguments("__value", false, false)]
    [Arguments("_Value", false, false)]
    [Arguments("_1", false, false)]
    public async Task UnderscoreFormsAreDistinguishedAsync(string name, bool allUnderscores, bool camelField)
    {
        await Assert.That(NamingHelper.IsAllUnderscores(name)).IsEqualTo(allUnderscores);
        await Assert.That(NamingHelper.IsUnderscoreCamelCase(name)).IsEqualTo(camelField);
    }

    /// <summary>Checks simple suggestions strip known prefixes while preserving the rest of the name.</summary>
    /// <param name="name">The original name.</param>
    /// <param name="pascal">The expected PascalCase suggestion.</param>
    /// <param name="camel">The expected camelCase suggestion.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "", "")]
    [Arguments("_", "_", "_")]
    [Arguments("___", "___", "___")]
    [Arguments("m_", "m_", "m_")]
    [Arguments("s_", "s_", "s_")]
    [Arguments("t_", "t_", "t_")]
    [Arguments("x", "X", "x")]
    [Arguments("X", "X", "x")]
    [Arguments("value", "Value", "value")]
    [Arguments("Value", "Value", "value")]
    [Arguments("__value", "Value", "value")]
    [Arguments("m_value", "Value", "value")]
    [Arguments("s_Value", "Value", "value")]
    [Arguments("t__Value", "Value", "value")]
    [Arguments("q_value", "Q_value", "q_value")]
    public async Task SimpleSuggestionsStripKnownPrefixesAsync(string name, string pascal, string camel)
    {
        await Assert.That(NamingHelper.SuggestPascalCase(name)).IsEqualTo(pascal);
        await Assert.That(NamingHelper.SuggestCamelCase(name)).IsEqualTo(camel);
        await Assert.That(NamingHelper.SuggestUnderscoreCamelCase(name)).IsEqualTo($"_{camel}");
        await Assert.That(NamingHelper.SuggestPrefixed(name, 'I')).IsEqualTo($"I{pascal}");
    }

    /// <summary>Checks full-name suggestions handle word separators and acronym boundaries.</summary>
    /// <param name="name">The original name.</param>
    /// <param name="expected">The expected full-name suggestion.</param>
    /// <param name="alreadyPascal">Whether the original name conforms.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "", false)]
    [Arguments("___", "___", false)]
    [Arguments("m_", "m_", false)]
    [Arguments("value_name", "ValueName", false)]
    [Arguments("Value_Name", "ValueName", false)]
    [Arguments("Value__name_", "ValueName", false)]
    [Arguments("IOMode", "IOMode", true)]
    [Arguments("MyIO", "MyIO", true)]
    [Arguments("HTTPStatus", "HttpStatus", false)]
    [Arguments("MyENUM", "MyEnum", false)]
    [Arguments("Value1", "Value1", true)]
    public async Task FullSuggestionsNormalizeWordsAndAcronymsAsync(string name, string expected, bool alreadyPascal)
    {
        await Assert.That(NamingHelper.IsPascalCase(name)).IsEqualTo(alreadyPascal);
        await Assert.That(NamingHelper.SuggestPascalCaseName(name)).IsEqualTo(expected);
    }
}
