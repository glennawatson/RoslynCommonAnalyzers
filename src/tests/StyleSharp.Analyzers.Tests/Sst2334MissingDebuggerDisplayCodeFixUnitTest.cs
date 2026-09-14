// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using VerifyDisplay = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2334MissingDebuggerDisplayAnalyzer,
    StyleSharp.Analyzers.Sst2334MissingDebuggerDisplayCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2334MissingDebuggerDisplayCodeFixProvider"/> (SST2334 add [DebuggerDisplay]).</summary>
public class Sst2334MissingDebuggerDisplayCodeFixUnitTest
{
    /// <summary>A public type with a public property to name.</summary>
    private const string WithPropertySource = """
        public class {|SST2334:Money|}
        {
            public int Amount { get; set; }
        }
        """;

    /// <summary>The type after the fix leads with the type name and names its first public property.</summary>
    private const string WithPropertyFixed = """
        [System.Diagnostics.DebuggerDisplay("Money: {Amount}")]
        public class Money
        {
            public int Amount { get; set; }
        }
        """;

    /// <summary>A public type whose only state is a private field.</summary>
    private const string NoPropertySource = """
        public class {|SST2334:Money|}
        {
            private int _amount;

            public int Read() => _amount;
        }
        """;

    /// <summary>The type after the fix names the field, which a display string may read in the type's own context.</summary>
    private const string NoPropertyFixed = """
        [System.Diagnostics.DebuggerDisplay("Money: {_amount}")]
        public class Money
        {
            private int _amount;

            public int Read() => _amount;
        }
        """;

    /// <summary>A public type whose only property is implemented explicitly, so it is not nameable.</summary>
    private const string ExplicitImplementationSource = """
        public interface IHolder
        {
            object Value { get; }
        }

        public class {|SST2334:Holder|} : IHolder
        {
            object IHolder.Value => new object();
        }
        """;

    /// <summary>The type after the fix falls back to <c>ToString()</c> rather than naming the explicit member.</summary>
    private const string ExplicitImplementationFixed = """
        public interface IHolder
        {
            object Value { get; }
        }

        [System.Diagnostics.DebuggerDisplay("Holder: {ToString(),nq}")]
        public class Holder : IHolder
        {
            object IHolder.Value => new object();
        }
        """;

    /// <summary>A public type whose only readable member is its own <c>ToString()</c>.</summary>
    private const string ToStringOnlySource = """
        public class {|SST2334:Money|}
        {
            public override string ToString() => "money";
        }
        """;

    /// <summary>The type after the fix falls back to <c>ToString()</c>.</summary>
    private const string ToStringOnlyFixed = """
        [System.Diagnostics.DebuggerDisplay("Money: {ToString(),nq}")]
        public class Money
        {
            public override string ToString() => "money";
        }
        """;

    /// <summary>A public type whose only property is not publicly exposed.</summary>
    private const string NonPublicPropertySource = """
        public class {|SST2334:Money|}
        {
            internal int Amount { get; set; }
        }
        """;

    /// <summary>The type after the fix names the internal property in preference to <c>ToString()</c>.</summary>
    private const string NonPublicPropertyFixed = """
        [System.Diagnostics.DebuggerDisplay("Money: {Amount}")]
        public class Money
        {
            internal int Amount { get; set; }
        }
        """;

    /// <summary>A public generic type, whose prefix must not carry its type parameter list.</summary>
    private const string GenericSource = """
        public class {|SST2334:Box|}<T>
        {
            public T Value { get; set; }
        }
        """;

    /// <summary>The type after the fix, prefixed with the bare type name.</summary>
    private const string GenericFixed = """
        [System.Diagnostics.DebuggerDisplay("Box: {Value}")]
        public class Box<T>
        {
            public T Value { get; set; }
        }
        """;

    /// <summary>Verifies the fix names the type's first public property in the display string.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsAttributeNamingFirstPropertyAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(WithPropertySource, WithPropertyFixed);

    /// <summary>Verifies the fix names a private field rather than saying nothing about the instance.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsAttributeNamingPrivateFieldAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(NoPropertySource, NoPropertyFixed);

    /// <summary>Verifies the fix prefers a non-public property over the <c>ToString()</c> fallback.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsAttributeNamingNonPublicPropertyAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(NonPublicPropertySource, NonPublicPropertyFixed);

    /// <summary>Verifies the fix falls back to <c>ToString()</c> when the type has no member to name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsAttributeFallingBackToToStringAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(ToStringOnlySource, ToStringOnlyFixed);

    /// <summary>Verifies a generic type is prefixed with its bare name, without the type parameter list.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsAttributePrefixedWithBareGenericNameAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(GenericSource, GenericFixed);

    /// <summary>Verifies an explicitly implemented property is not named, because the display string cannot read it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// A display string binds in the type's own context, where an explicit implementation is reachable only
    /// through a cast to the interface. Naming it produces an attribute that points at nothing.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DoesNotNameAnExplicitlyImplementedPropertyAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(ExplicitImplementationSource, ExplicitImplementationFixed);

    /// <summary>Verifies member ranking excludes static and unreadable members and retains the first member at each rank.</summary>
    /// <param name="members">The candidate members in declaration order.</param>
    /// <param name="expression">The member expression selected for the display string.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public int First { get; } public int Second { get; }", "{First}")]
    [Arguments("public int Field; internal int Internal { get; } public int Public { get; }", "{Public}")]
    [Arguments("public int Public { get; } internal int Internal { get; } public int Field;", "{Public}")]
    [Arguments("public static int Static { get; } public const int Constant = 1; public int Instance => 2;", "{Instance}")]
    [Arguments("public int WriteOnly { set { } } public int Readable => 1;", "{Readable}")]
    [Arguments("public int Reordered { set { } get { return 1; } }", "{Reordered}")]
    [Arguments("public int WriteOnly { set { } } public override string ToString() => string.Empty;", "{ToString(),nq}")]
    [Arguments("public int this[int index] => index; public int Value => 1;", "{Value}")]
    [Arguments("public int First, Second;", "{First}")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task BestReadableInstanceMemberIsNamedAsync(string members, string expression) =>
        VerifyDisplay.VerifyCodeFixAsync(
            $$"""
            public class {|SST2334:Money|}
            {
                {{members}}
            }
            """,
            $$"""
            [System.Diagnostics.DebuggerDisplay("Money: {{expression}}")]
            public class Money
            {
                {{members}}
            }
            """);

    /// <summary>Verifies documentation and existing attributes remain attached to an indented type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NamespacedTypeRetainsDocumentationIndentationAndAttributesAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(
            """
            namespace Values
            {
                /// <summary>An amount.</summary>
                [System.Serializable]
                public class {|SST2334:Money|}
                {
                    public int Amount { get; }
                }
            }
            """,
            """
            namespace Values
            {
                /// <summary>An amount.</summary>
                [System.Diagnostics.DebuggerDisplay("Money: {Amount}")]
                [System.Serializable]
                public class Money
                {
                    public int Amount { get; }
                }
            }
            """);

    /// <summary>Verifies a top-level comment moves above the new attribute without becoming indentation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LeadingCommentRemainsAboveAttributeAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(
            """
            // Amount
            public class {|SST2334:Money|}
            {
                public int Amount { get; }
            }
            """,
            """
            // Amount
            [System.Diagnostics.DebuggerDisplay("Money: {Amount}")]
            public class Money
            {
                public int Amount { get; }
            }
            """);

    /// <summary>Verifies diagnostics outside a type declaration do not offer an attribute.</summary>
    /// <param name="source">The declaration replacing the previously diagnosed type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("enum C { }")]
    [Arguments("namespace C { }")]
    public async Task RemovedTypeDoesNotRegisterAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var identifier = root.DescendantTokens().Single(static token => token.ValueText == "C");
        var diagnostic = Diagnostic.Create(DesignRules.MissingDebuggerDisplay, identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2334MissingDebuggerDisplayCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Verifies an incomplete property without an accessor list cannot be named.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PropertyWithoutAccessorsFallsBackToToStringAsync() =>
        VerifyFallbackAsync(SyntaxFactory.PropertyDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)), "Value"));

    /// <summary>Verifies an incomplete field without variables cannot be named.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task FieldWithoutVariablesFallsBackToToStringAsync() =>
        VerifyFallbackAsync(SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)))));

    /// <summary>Verifies a stale diagnostic on an empty type still yields the fallback display string.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RemovedMembersFallBackToToStringAsync() =>
        VerifyFallbackAsync();

    /// <summary>Applies the registered fix to an incomplete syntax tree and checks the fallback attribute.</summary>
    /// <param name="members">The remaining members of the diagnosed type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    private static async Task VerifyFallbackAsync(params MemberDeclarationSyntax[] members)
    {
        using var workspace = new AdhocWorkspace();
        var declaration = SyntaxFactory.ClassDeclaration("C").AddMembers(members);
        var root = SyntaxFactory.CompilationUnit().AddMembers(declaration);
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", string.Empty).WithSyntaxRoot(root);
        var currentRoot = (await document.GetSyntaxRootAsync())!;
        var currentDeclaration = currentRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        var diagnostic = Diagnostic.Create(DesignRules.MissingDebuggerDisplay, currentDeclaration.Identifier.GetLocation());
        using var container = new ContainerConfiguration().WithPart<Sst2334MissingDebuggerDisplayCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions.Count).IsEqualTo(1);
        var operations = await actions[0].GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
        var changedRoot = (await changed.GetSyntaxRootAsync())!;
        var attribute = changedRoot.DescendantNodes().OfType<AttributeSyntax>().Single();
        await Assert.That(attribute.Name.ToString()).IsEqualTo("System.Diagnostics.DebuggerDisplay");
        var display = (LiteralExpressionSyntax)attribute.ArgumentList!.Arguments[0].Expression;
        await Assert.That(display.Token.ValueText).IsEqualTo("C: {ToString(),nq}");
    }
}
