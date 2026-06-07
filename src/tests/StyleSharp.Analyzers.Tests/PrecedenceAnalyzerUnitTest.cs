// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for precedence rule classification helpers.</summary>
public sealed class PrecedenceAnalyzerUnitTest
{
    /// <summary>Verifies mixed arithmetic families select the arithmetic precedence rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelectRuleReportsMixedArithmeticFamiliesAsync()
    {
        var rule = PrecedenceAnalyzer.SelectRule(SyntaxKind.MultiplyExpression, SyntaxKind.AddExpression);

        await Assert.That(rule).IsNotNull();
        await Assert.That(rule!.Id).IsEqualTo(MaintainabilityRules.ArithmeticPrecedence.Id);
    }

    /// <summary>Verifies mixed conditional operators select the conditional precedence rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelectRuleReportsMixedConditionalOperatorsAsync()
    {
        var rule = PrecedenceAnalyzer.SelectRule(SyntaxKind.LogicalAndExpression, SyntaxKind.LogicalOrExpression);

        await Assert.That(rule).IsNotNull();
        await Assert.That(rule!.Id).IsEqualTo(MaintainabilityRules.ConditionalPrecedence.Id);
    }

    /// <summary>Verifies matching precedence families remain clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SelectRuleSkipsMatchingFamiliesAsync()
    {
        await Assert.That(PrecedenceAnalyzer.SelectRule(SyntaxKind.AddExpression, SyntaxKind.SubtractExpression)).IsNull();
        await Assert.That(PrecedenceAnalyzer.SelectRule(SyntaxKind.LogicalOrExpression, SyntaxKind.LogicalOrExpression)).IsNull();
    }

    /// <summary>Verifies operator classification groups arithmetic families together for cheap precedence checks.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassifyOperatorMapsArithmeticFamiliesAsync()
    {
        await Assert.That(PrecedenceAnalyzer.ClassifyOperator(SyntaxKind.MultiplyExpression)).IsEqualTo(3);
        await Assert.That(PrecedenceAnalyzer.ClassifyOperator(SyntaxKind.AddExpression)).IsEqualTo(4);
        await Assert.That(PrecedenceAnalyzer.ClassifyOperator(SyntaxKind.LeftShiftExpression)).IsEqualTo(5);
    }

    /// <summary>Verifies operator classification distinguishes the two conditional operators.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassifyOperatorMapsConditionalOperatorsAsync()
    {
        await Assert.That(PrecedenceAnalyzer.ClassifyOperator(SyntaxKind.LogicalAndExpression)).IsEqualTo(1);
        await Assert.That(PrecedenceAnalyzer.ClassifyOperator(SyntaxKind.LogicalOrExpression)).IsEqualTo(2);
    }
}
