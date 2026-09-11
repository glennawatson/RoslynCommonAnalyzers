// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Tests the shared descriptor factory used by the unique-lines analyzer family. The family's
/// <c>Register</c> wiring is exercised end-to-end by each per-syntax-kind analyzer's own verifier tests,
/// which drive a real analysis through it.
/// </summary>
public sealed class UniqueLineRuleUnitTest
{
    /// <summary>The family's constructor-declaration id, used to prove a descriptor carries the id it was built with.</summary>
    private const string ConstructorParametersRuleId = "SST1150";

    /// <summary>Verifies the parameter descriptor carries the requested id and the family's fixed metadata.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForParametersCarriesRequestedIdAndFamilyMetadataAsync()
    {
        var rule = UniqueLineRule.ForParameters(ConstructorParametersRuleId);

        await Assert.That(rule.Id).IsEqualTo(ConstructorParametersRuleId);
        await Assert.That(rule.Category).IsEqualTo("Readability");
        await Assert.That(rule.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(rule.IsEnabledByDefault).IsTrue();
        await Assert.That(rule.HelpLinkUri).Contains(ConstructorParametersRuleId);
    }

    /// <summary>Verifies the argument descriptor carries its id and is worded differently from the parameter descriptor.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForArgumentsWordsItsTitleDifferentlyFromForParametersAsync()
    {
        var parameters = UniqueLineRule.ForParameters(ConstructorParametersRuleId);
        var arguments = UniqueLineRule.ForArguments("SST1154");

        await Assert.That(arguments.Id).IsEqualTo("SST1154");
        await Assert.That(arguments.Title.ToString()).IsNotEqualTo(parameters.Title.ToString());
    }
}
