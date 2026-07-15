// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>
/// Direct tests for <see cref="SealedModifierRewrite"/>, the <c>sealed</c> insertion shared by the
/// PSH1401 and PSH1411 code fixes.
/// </summary>
public class SealedModifierRewriteUnitTest
{
    /// <summary>Verifies <c>sealed</c> lands after an accessibility modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddSealedInsertsAfterAccessibilityAsync()
    {
        var sealed_ = SealedModifierRewrite.AddSealed(Class("public class C { }"));

        await Assert.That(ModifierText(sealed_)).IsEqualTo("public sealed");
    }

    /// <summary>Verifies <c>sealed</c> is the sole modifier when none were present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddSealedBecomesTheOnlyModifierWhenNoneExistAsync()
    {
        var sealed_ = SealedModifierRewrite.AddSealed(Class("class C { }"));

        await Assert.That(ModifierText(sealed_)).IsEqualTo("sealed");
        await Assert.That(sealed_.ToString()).Contains("sealed class C");
    }

    /// <summary>Verifies <c>sealed</c> goes before <c>partial</c> when no accessibility modifier is present.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddSealedGoesBeforePartialWithoutAccessibilityAsync()
    {
        var sealed_ = SealedModifierRewrite.AddSealed(Class("partial class C { }"));

        await Assert.That(ModifierText(sealed_)).IsEqualTo("sealed partial");
    }

    /// <summary>Verifies <c>sealed</c> lands between accessibility and <c>partial</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddSealedGoesBetweenAccessibilityAndPartialAsync()
    {
        var sealed_ = SealedModifierRewrite.AddSealed(Class("public partial class C { }"));

        await Assert.That(ModifierText(sealed_)).IsEqualTo("public sealed partial");
    }

    /// <summary>Parses a class declaration.</summary>
    /// <param name="text">The declaration source.</param>
    /// <returns>The parsed class declaration.</returns>
    private static ClassDeclarationSyntax Class(string text)
        => (ClassDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(text)!;

    /// <summary>Renders a declaration's modifiers as a space-separated string.</summary>
    /// <param name="declaration">The class declaration.</param>
    /// <returns>The modifier keywords joined by single spaces.</returns>
    private static string ModifierText(ClassDeclarationSyntax declaration)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var modifier in declaration.Modifiers)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(modifier.Text);
        }

        return builder.ToString();
    }
}
