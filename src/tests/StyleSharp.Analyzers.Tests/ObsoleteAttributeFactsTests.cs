// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests obsolete attribute names, declaration names, and message classification.</summary>
public class ObsoleteAttributeFactsTests
{
    /// <summary>Verifies the name fallback treats a missing name as unrelated to obsolete attributes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingAttributeNameIsNotObsoleteAsync()
    {
        var result = ObsoleteAttributeFacts.IsObsoleteName(null!);
        await Assert.That(result).IsFalse();
    }

    /// <summary>Verifies simple, qualified, and aliased obsolete names are recognized syntactically.</summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="expected">Whether the simple name is obsolete.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Obsolete", true)]
    [Arguments("ObsoleteAttribute", true)]
    [Arguments("System.Obsolete", true)]
    [Arguments("global::Obsolete", true)]
    [Arguments("global::Other", false)]
    [Arguments("Other", false)]
    public async Task WrittenNameUsesRightmostIdentifierAsync(string name, bool expected)
    {
        var syntax = SyntaxFactory.ParseName(name);
        await Assert.That(ObsoleteAttributeFacts.IsObsoleteName(syntax)).IsEqualTo(expected);
    }

    /// <summary>Verifies the less common annotated declarations retain their source name.</summary>
    /// <param name="source">The annotated source declaration.</param>
    /// <param name="expected">The name presented by the obsolete rules.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { [A] event System.Action Changed { add {} remove {} } }", "Changed")]
    [Arguments("class C { [A] int this[int index] => 0; }", "this")]
    [Arguments("enum E { [A] Value }", "Value")]
    [Arguments("class C { void M() { [A] void Local() {} } }", "Local")]
    [Arguments("class C { [A] public static C operator +(C a, C b) => a; }", "+")]
    [Arguments("class C { void M([A] int value) {} }", "value")]
    [Arguments("class C { [A] public static implicit operator int(C value) => 0; }", "")]
    [Arguments("class C { [A] int first, second; }", "first")]
    [Arguments("class C { [A] event System.Action first, second; }", "first")]
    public async Task AnnotatedDeclarationUsesItsOwnNameAsync(string source, string expected)
    {
        var root = SyntaxFactory.ParseCompilationUnit(source);
        var attribute = root.DescendantNodes().OfType<AttributeSyntax>().Single();
        await Assert.That(ObsoleteAttributeFacts.GetAnnotatedName(attribute.Parent!.Parent)).IsEqualTo(expected);
    }

    /// <summary>Verifies missing declarations and an incomplete field have no name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingDeclarationAndEmptyFieldHaveNoNameAsync()
    {
        var field = SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword))));
        await Assert.That(ObsoleteAttributeFacts.GetAnnotatedName(null)).IsEqualTo(string.Empty);
        await Assert.That(ObsoleteAttributeFacts.GetAnnotatedName(field)).IsEqualTo(string.Empty);
    }

    /// <summary>Verifies literal, named, constant, and nonconstant message arguments follow the current contract.</summary>
    /// <param name="arguments">The attribute argument list, if present.</param>
    /// <param name="expected">Whether no usable message is supplied.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", true)]
    [Arguments("()", true)]
    [Arguments("(error: true)", true)]
    [Arguments("(DiagnosticId = \"ID1\")", true)]
    [Arguments("(error: true, message: \" \" )", true)]
    [Arguments("(\"\")", true)]
    [Arguments("(\"\\t \\r\\n\")", true)]
    [Arguments("(null)", true)]
    [Arguments("(Blank)", true)]
    [Arguments("(Missing)", true)]
    [Arguments("(\"use replacement\")", false)]
    [Arguments("(Message)", false)]
    [Arguments("(Number)", false)]
    [Arguments("(GetMessage())", false)]
    public async Task MessageClassificationDistinguishesBlankFromNonconstantAsync(string arguments, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            class C
            {
                const string Blank = " ";
                const string Missing = null;
                const string Message = "Use replacement";
                const int Number = 1;
                static string GetMessage() => "replacement";
                [System.Obsolete{{arguments}}] void M() {}
            }
            """);
        var compilation = CSharpCompilation.Create("ObsoleteMessages", [tree], RuntimeMetadataReferences.Platform);
        var attribute = (await tree.GetRootAsync()).DescendantNodes().OfType<AttributeSyntax>().Single();
        await Assert.That(ObsoleteAttributeFacts.HasNoUsableMessage(compilation.GetSemanticModel(tree), attribute, CancellationToken.None)).IsEqualTo(expected);
    }

    /// <summary>Verifies constructor binding fallback and namespace identity distinguish the framework attribute.</summary>
    /// <param name="source">The attribute declaration and use.</param>
    /// <param name="expected">Whether the attribute is the framework obsolete type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("[System.Obsolete] class C {}", true)]
    [Arguments("[System.Obsolete(1)] class C {}", true)]
    [Arguments("[Missing] class C {}", false)]
    [Arguments("[System.Serializable] class C {}", false)]
    [Arguments("class ObsoleteAttribute : System.Attribute {} [Obsolete] class C {}", false)]
    [Arguments("namespace Other.System { class ObsoleteAttribute : global::System.Attribute {} [Obsolete] class C {} }", false)]
    public async Task FrameworkAttributeRequiresItsExactNamespaceAsync(string source, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("ObsoleteBinding", [tree], RuntimeMetadataReferences.Platform);
        var attribute = (await tree.GetRootAsync()).DescendantNodes().OfType<AttributeSyntax>().Single();
        await Assert.That(ObsoleteAttributeFacts.IsFrameworkObsoleteAttribute(compilation.GetSemanticModel(tree), attribute, CancellationToken.None)).IsEqualTo(expected);
    }

    /// <summary>Verifies only the requested property initializer counts, regardless of other argument forms.</summary>
    /// <param name="arguments">The attribute arguments.</param>
    /// <param name="expected">Whether DiagnosticId is explicitly initialized.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", false)]
    [Arguments("()", false)]
    [Arguments("(message: \"use new\", UrlFormat = \"url\")", false)]
    [Arguments("(\"use new\", DiagnosticId = \"ID1\")", true)]
    public async Task PropertyInitializerMatchesExactNameAsync(string arguments, bool expected)
    {
        var root = SyntaxFactory.ParseCompilationUnit($"[System.Obsolete{arguments}] class C {{}}");
        var attribute = root.DescendantNodes().OfType<AttributeSyntax>().Single();
        await Assert.That(ObsoleteAttributeFacts.HasPropertyInitializer(attribute, ObsoleteAttributeFacts.DiagnosticIdPropertyName)).IsEqualTo(expected);
    }
}
