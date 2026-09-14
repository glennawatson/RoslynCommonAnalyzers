// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using RoslynCommon.Analyzers.Tests;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared identifier searches by name, by parameter and by bound symbol.</summary>
public sealed class IdentifierReferencesUnitTest
{
    /// <summary>The name shared by the field and the second method's parameter.</summary>
    private const string ValueName = "value";

    /// <summary>The first parameter of the first method.</summary>
    private const string FirstName = "first";

    /// <summary>A name that appears nowhere in the source.</summary>
    private const string MissingName = "missing";

    /// <summary>The source every test searches.</summary>
    private const string Source =
        """
        class C
        {
            int value;

            void M(int first, int second)
            {
                var local = first + 1;
                value = local;
                System.Console.WriteLine(second);
            }

            void N(int value) { value++; }
        }
        """;

    /// <summary>The parsed source.</summary>
    private static readonly SyntaxTree Tree = CSharpSyntaxTree.ParseText(Source);

    /// <summary>The model binding the source.</summary>
    private static readonly SemanticModel Model = CSharpCompilation.Create(nameof(IdentifierReferencesUnitTest), [Tree], RuntimeMetadataReferences.Platform).GetSemanticModel(Tree);

    /// <summary>Verifies a name search matches the node itself or any descendant identifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MentionsNameMatchesSelfAndDescendantsAsync()
    {
        var body = (await MethodAsync("M")).Body!;
        var first = FirstIdentifier(body, FirstName);
        await Assert.That(IdentifierReferences.MentionsName(body, FirstName)).IsTrue();
        await Assert.That(IdentifierReferences.MentionsName(body, MissingName)).IsFalse();
        await Assert.That(IdentifierReferences.MentionsName(first, FirstName)).IsTrue();
        await Assert.That(IdentifierReferences.MentionsName(first, ValueName)).IsFalse();
    }

    /// <summary>Verifies a parameter search matches any parameter's name, on the node itself or a descendant.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MentionsParameterMatchesAnyParameterAsync()
    {
        var method = await MethodAsync("M");
        var statements = method.Body!.Statements;
        await Assert.That(IdentifierReferences.MentionsParameter(statements[0], method.ParameterList)).IsTrue();
        await Assert.That(IdentifierReferences.MentionsParameter(statements[1], method.ParameterList)).IsFalse();
        await Assert.That(IdentifierReferences.MentionsParameter(FirstIdentifier(statements[0], FirstName), method.ParameterList)).IsTrue();
        await Assert.That(IdentifierReferences.MentionsParameter(FirstIdentifier(statements[1], ValueName), method.ParameterList)).IsFalse();
    }

    /// <summary>Verifies a symbol search binds same-named identifiers and rejects a different symbol with the same name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferencesBindsSameNamedIdentifiersAsync()
    {
        var root = await Tree.GetRootAsync();
        var field = Model.GetDeclaredSymbol(root.DescendantNodes().OfType<VariableDeclaratorSyntax>().First())!;
        var withField = (await MethodAsync("M")).Body!;
        var withParameter = await MethodAsync("N");
        var parameter = Model.GetDeclaredSymbol(withParameter.ParameterList.Parameters[0])!;
        var fieldWrite = FirstIdentifier(withField, ValueName);
        var parameterWrite = FirstIdentifier(withParameter.Body!, ValueName);
        await Assert.That(IdentifierReferences.References(withField, field, Model, CancellationToken.None)).IsTrue();
        await Assert.That(IdentifierReferences.References(withParameter.Body!, field, Model, CancellationToken.None)).IsFalse();
        await Assert.That(IdentifierReferences.References(fieldWrite, field, Model, CancellationToken.None)).IsTrue();
        await Assert.That(IdentifierReferences.IsReferenceTo(parameterWrite, parameter, Model, CancellationToken.None)).IsTrue();
        await Assert.That(IdentifierReferences.IsReferenceTo(parameterWrite, field, Model, CancellationToken.None)).IsFalse();
        await Assert.That(IdentifierReferences.IsReferenceTo(FirstIdentifier(withField, FirstName), field, Model, CancellationToken.None)).IsFalse();
    }

    /// <summary>Verifies an identifier-token search also finds declared names that no identifier name references.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsIdentifierTokenFindsDeclaredNamesAsync()
    {
        var field = (await Tree.GetRootAsync()).DescendantNodes().OfType<FieldDeclarationSyntax>().First();
        await Assert.That(IdentifierReferences.ContainsIdentifierToken(field, ValueName)).IsTrue();
        await Assert.That(IdentifierReferences.MentionsName(field, ValueName)).IsFalse();
        await Assert.That(IdentifierReferences.ContainsIdentifierToken(field, MissingName)).IsFalse();
    }

    /// <summary>Verifies a member-receiver search accepts a name only ever read through a member access.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsOnlyMemberReceiverRejectsOtherUsesAsync()
    {
        var body = (await MethodAsync("M")).Body!;
        await Assert.That(IdentifierReferences.IsOnlyMemberReceiver(body, "System")).IsTrue();
        await Assert.That(IdentifierReferences.IsOnlyMemberReceiver(body, "second")).IsFalse();
        await Assert.That(IdentifierReferences.IsOnlyMemberReceiver(body, MissingName)).IsTrue();
    }

    /// <summary>Gets a method declaration by name.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The method declaration.</returns>
    private static async Task<MethodDeclarationSyntax> MethodAsync(string name) =>
        (await Tree.GetRootAsync()).DescendantNodes().OfType<MethodDeclarationSyntax>().First(method => method.Identifier.ValueText == name);

    /// <summary>Gets the first identifier with a name beneath a node.</summary>
    /// <param name="node">The node to search.</param>
    /// <param name="name">The identifier name.</param>
    /// <returns>The identifier.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IdentifierNameSyntax FirstIdentifier(SyntaxNode node, string name) =>
        node.DescendantNodes().OfType<IdentifierNameSyntax>().First(identifier => identifier.Identifier.ValueText == name);
}
