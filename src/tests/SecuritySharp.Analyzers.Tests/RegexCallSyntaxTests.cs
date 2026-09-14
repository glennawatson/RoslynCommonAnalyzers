// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Tests the syntax gates and binding checks for regex constructions and static regex calls.</summary>
public class RegexCallSyntaxTests
{
    /// <summary>A compilation unit with regex and look-alike calls.</summary>
    private const string Source =
        """
        using System.Text.RegularExpressions;
        class C
        {
            void M(string s)
            {
                _ = new Regex(s);
                _ = Regex.IsMatch(s, s);
                _ = new Regex(s).IsMatch(s);
                _ = Other.IsMatch(s, s);
                _ = new Other(s);
            }
        }
        class Other
        {
            public Other(string s) { }
            public static bool IsMatch(string a, string b) => true;
        }
        """;

    /// <summary>Verifies the construction gate needs a type written as <c>Regex</c> and at least one argument.</summary>
    /// <param name="creation">The object creation text.</param>
    /// <param name="expected">Whether the gate passes.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("new Regex(p)", true)]
    [Arguments("new System.Text.RegularExpressions.Regex(p)", true)]
    [Arguments("new global::System.Text.RegularExpressions.Regex(p)", true)]
    [Arguments("new Regex()", false)]
    [Arguments("new Regex { }", false)]
    [Arguments("new Other(p)", false)]
    public async Task CreationGateNeedsRegexAndAnArgumentAsync(string creation, bool expected)
    {
        var syntax = (ObjectCreationExpressionSyntax)SyntaxFactory.ParseExpression(creation);

        await Assert.That(RegexCallSyntax.TryGetCreationArguments(syntax, out var argumentList)).IsEqualTo(expected);
        await Assert.That(argumentList is not null).IsEqualTo(expected);
    }

    /// <summary>Verifies the call gate needs a member call passing an input and a pattern.</summary>
    /// <param name="call">The invocation text.</param>
    /// <param name="expected">The member name when the gate passes, or an empty string.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Regex.IsMatch(a, b)", "IsMatch")]
    [Arguments("Regex.Replace(a, b, c)", "Replace")]
    [Arguments("anything.Else(a, b)", "Else")]
    [Arguments("Regex.IsMatch(a)", "")]
    [Arguments("IsMatch(a, b)", "")]
    public async Task CallGateNeedsAMemberCallWithInputAndPatternAsync(string call, string expected)
    {
        var invocation = (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(call);

        await Assert.That(RegexCallSyntax.TryGetStaticCallName(invocation, out var name)).IsEqualTo(expected.Length > 0);
        await Assert.That(name ?? string.Empty).IsEqualTo(expected);
    }

    /// <summary>Verifies only the shared pattern methods are recognized.</summary>
    /// <param name="name">The method name.</param>
    /// <param name="expected">Whether the name is a shared pattern method.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("IsMatch", true)]
    [Arguments("Match", true)]
    [Arguments("Matches", true)]
    [Arguments("Replace", true)]
    [Arguments("Split", true)]
    [Arguments("Count", false)]
    [Arguments("Escape", false)]
    public async Task PatternMethodNamesAreTheSharedSetAsync(string name, bool expected) =>
        await Assert.That(RegexCallSyntax.IsPatternMethodName(name)).IsEqualTo(expected);

    /// <summary>Verifies the binding checks accept only a <c>Regex</c> constructor and a static <c>Regex</c> method.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BindingAcceptsOnlyTheRegexTypeAsync()
    {
        var tree = CSharpSyntaxTree.ParseText(Source);
        var compilation = CSharpCompilation.Create(nameof(BindingAcceptsOnlyTheRegexTypeAsync), [tree], RuntimeMetadataReferences.Platform);
        var model = compilation.GetSemanticModel(tree);
        var regex = compilation.GetTypeByMetadataName("System.Text.RegularExpressions.Regex")!;
        var root = await tree.GetRootAsync();
        var creations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().ToArray();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();

        await Assert.That(RegexCallSyntax.TryBindConstructor(model, creations[0], regex, CancellationToken.None, out var constructor)).IsTrue();
        await Assert.That(constructor).IsNotNull();
        await Assert.That(RegexCallSyntax.TryBindConstructor(model, creations[^1], regex, CancellationToken.None, out _)).IsFalse();
        await Assert.That(RegexCallSyntax.TryBindStaticMethod(model, invocations[0], regex, CancellationToken.None, out var method)).IsTrue();
        await Assert.That(method).IsNotNull();
        await Assert.That(RegexCallSyntax.TryBindStaticMethod(model, invocations[1], regex, CancellationToken.None, out _)).IsFalse();
        await Assert.That(RegexCallSyntax.TryBindStaticMethod(model, invocations[^1], regex, CancellationToken.None, out _)).IsFalse();
    }
}
