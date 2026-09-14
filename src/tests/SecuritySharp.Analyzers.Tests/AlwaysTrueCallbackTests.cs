// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Tests the syntactic classification of an always-true callback value.</summary>
public class AlwaysTrueCallbackTests
{
    /// <summary>Verifies lambdas are decided by their body and names are left for binding.</summary>
    /// <param name="value">The callback value text.</param>
    /// <param name="expected">The expected shape, as its underlying value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("_ => true", (int)AlwaysTrueCallbackShape.AlwaysTrueLambda)]
    [Arguments("(a, b, c, d) => (true)", (int)AlwaysTrueCallbackShape.AlwaysTrueLambda)]
    [Arguments("delegate { return true; }", (int)AlwaysTrueCallbackShape.AlwaysTrueLambda)]
    [Arguments("_ => false", (int)AlwaysTrueCallbackShape.None)]
    [Arguments("_ => { if (x) { return false; } return true; }", (int)AlwaysTrueCallbackShape.None)]
    [Arguments("Validate", (int)AlwaysTrueCallbackShape.MethodGroup)]
    [Arguments("Validators.Validate", (int)AlwaysTrueCallbackShape.MethodGroup)]
    [Arguments("validators?.Validate", (int)AlwaysTrueCallbackShape.None)]
    [Arguments("null", (int)AlwaysTrueCallbackShape.None)]
    [Arguments("Create()", (int)AlwaysTrueCallbackShape.None)]
    public async Task ValueIsClassifiedBeforeBindingAsync(string value, int expected) =>
        await Assert.That(AlwaysTrueCallback.Classify(SyntaxFactory.ParseExpression(value))).IsEqualTo((AlwaysTrueCallbackShape)expected);
}
