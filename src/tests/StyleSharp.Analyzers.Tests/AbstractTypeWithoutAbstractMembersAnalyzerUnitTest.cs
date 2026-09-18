// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;
using VerifyAbstractType = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1496AbstractTypeWithoutAbstractMembersAnalyzer,
    StyleSharp.Analyzers.Sst1496AbstractTypeWithoutAbstractMembersCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1496 (an abstract type declares nothing abstract) and its fix.</summary>
public class AbstractTypeWithoutAbstractMembersAnalyzerUnitTest
{
    /// <summary>Verifies constraints on another type do not prevent sealing the generic class.</summary>
    /// <param name="constraint">The unrelated constraint on the type argument.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class")]
    [Arguments("System.Collections.Generic.IEnumerable<int>")]
    [Arguments("Other<int>")]
    [Arguments("Runner<int, int>")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnrelatedGenericConstraintAllowsSealingAsync(string constraint) =>
        VerifyAbstractType.VerifyCodeFixAsync(
            $$"""
            public class Other<T> { }
            public class Runner<TFirst, TSecond> { }
            public abstract class {|SST1496:Runner|}<T> where T : {{constraint}}
            {
                public int Value => 1;
            }
            """,
            $$"""
            public class Other<T> { }
            public class Runner<TFirst, TSecond> { }
            public sealed class Runner<T> where T : {{constraint}}
            {
                public int Value => 1;
            }
            """);

    /// <summary>Verifies removing abstract preserves a self-referential generic constraint.</summary>
    /// <param name="constraint">The spelling of the self-reference.</param>
    /// <param name="derived">Whether a concrete derived class exists.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MatrixDataSource]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SelfReferentialGenericBaseIsNotSealedAsync(
        [Matrix("Runner<T>", "global::Runner<T>")] string constraint,
        [Matrix("", "public class Schema : Runner<Schema> { }")] string derived) =>
        VerifyAbstractType.VerifyCodeFixAsync(
            $$"""
            public abstract class {|SST1496:Runner|}<T> where T : {{constraint}}, new()
            {
                public int Value => 1;
            }
            {{derived}}
            """,
            $$"""
            public class Runner<T> where T : {{constraint}}, new()
            {
                public int Value => 1;
            }
            {{derived}}
            """);

    /// <summary>Verifies generic constraints in members and consumers preserve an inheritable class.</summary>
    /// <param name="members">Members containing a possible generic constraint.</param>
    /// <param name="consumer">A separate declaration containing a possible generic constraint.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public void Run<T>() where T : Runner { }", "")]
    [Arguments("public class Nested<T> where T : Runner { }", "")]
    [Arguments("public int Value => 1;", "public class Consumer<T> where T : Runner { }")]
    [Arguments("public int Value => 1;", "public class Consumer { public void Run<T>() where T : Runner { } }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ClassUsedAsConstraintIsNotSealedAsync(string members, string consumer) =>
        VerifyAbstractType.VerifyCodeFixAsync(
            $$"""
            public abstract class {|SST1496:Runner|}
            {
                {{members}}
            }
            {{consumer}}
            """,
            $$"""
            public class Runner
            {
                {{members}}
            }
            {{consumer}}
            """);

    /// <summary>Verifies using a class as a generic argument does not require inheritance from that class.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ClassInsideConstraintTypeArgumentCanBeSealedAsync() =>
        VerifyAbstractType.VerifyCodeFixAsync(
            """
            public abstract class {|SST1496:Runner|} { public int Value => 1; }
            public interface IContainer<T> { }
            public class Consumer<T> where T : IContainer<Runner> { }
            """,
            """
            public sealed class Runner { public int Value => 1; }
            public interface IContainer<T> { }
            public class Consumer<T> where T : IContainer<Runner> { }
            """);

    /// <summary>Verifies constraint references in another document prevent sealing, including aliases.</summary>
    /// <param name="constraint">The consumer's spelling of the base constraint.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Runner")]
    [Arguments("Base")]
    public async Task ConstraintInAnotherDocumentPreventsSealingAsync(string constraint)
    {
        var test = new VerifyAbstractType.Test { TestCode = "public abstract class {|SST1496:Runner|} { public int Value => 1; }", FixedCode = "public class Runner { public int Value => 1; }" };
        var consumer = $$"""
            using Base = Runner;
            public class Consumer<T> where T : {{constraint}} { }
            """;
        test.TestState.Sources.Add(("Consumer.cs", consumer));
        test.FixedState.Sources.Add(("Consumer.cs", consumer));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies removing the first modifier preserves documentation on the next token.</summary>
    /// <param name="remainingModifier">The modifier that follows abstract, when present.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("public ")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RemovingFirstAbstractModifierKeepsDocumentationAsync(string remainingModifier) => VerifyAbstractType.VerifyCodeFixAsync(
        $$"""
        /// <summary>The extensible base.</summary>
        abstract {{remainingModifier}}class {|SST1496:C|}
        {
            public virtual void M() { }
        }
        """,
        $$"""
        /// <summary>The extensible base.</summary>
        {{remainingModifier}}class C
        {
            public virtual void M() { }
        }
        """);

    /// <summary>Verifies stale diagnostics on concrete or unrelated declarations offer no fix.</summary>
    /// <param name="source">The declaration at the diagnostic location.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { }")]
    [Arguments("public class C { }")]
    [Arguments("struct C { }")]
    [Arguments("abstract partial class C { }")]
    public async Task InapplicableDeclarationOffersNoFixAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("AbstractTypeFix", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From(source));
        var root = await document.GetSyntaxRootAsync();
        var diagnostic = Diagnostic.Create(MaintainabilityRules.AbstractTypeWithoutAbstractMembers, root!.GetFirstToken().GetLocation());
        var actions = new List<CodeAction>();
        using var container = new ContainerConfiguration().WithPart<Sst1496AbstractTypeWithoutAbstractMembersCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Verifies applying a stale fix to a concrete class preserves the document.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ApplyingToConcreteClassPreservesDocumentAsync()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("AbstractTypeFix", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "Test.cs", SourceText.From("public class C { }"));
        var root = await document.GetSyntaxRootAsync();
        var declaration = root!.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        var updated = Sst1496AbstractTypeWithoutAbstractMembersCodeFixProvider.Apply(document, root, declaration, seal: true);
        await Assert.That(updated).IsSameReferenceAs(document);
    }

    /// <summary>Verifies an abstract class with nothing abstract is reported, and sealing is the offered fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractClassWithNothingAbstractIsSealedAsync()
    {
        const string Source = """
                              public abstract class {|SST1496:Helper|}
                              {
                                  public int Add(int left, int right) => left + right;
                              }
                              """;
        const string FixedSource = """
                                   public sealed class Helper
                                   {
                                       public int Add(int left, int right) => left + right;
                                   }
                                   """;
        await VerifyAbstractType.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a class that declares an abstract member is a contract and is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AbstractMemberIsCleanAsync() =>
        VerifyAbstractType.VerifyAnalyzerAsync(
            """
            public abstract class Shape
            {
                public abstract double Area { get; }

                public override string ToString() => Area.ToString();
            }
            """);

    /// <summary>Verifies every kind of abstract member counts, not only methods.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EveryAbstractMemberKindCountsAsync() =>
        VerifyAbstractType.VerifyAnalyzerAsync(
            """
            using System;

            public abstract class WithMethod
            {
                public abstract void Run();
            }

            public abstract class WithIndexer
            {
                public abstract int this[int index] { get; }
            }

            public abstract class WithEvent
            {
                public abstract event EventHandler Changed;
            }
            """);

    /// <summary>Verifies a type that leaves an inherited abstract member unimplemented is genuinely abstract.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InheritedAbstractMemberIsCleanAsync() =>
        VerifyAbstractType.VerifyAnalyzerAsync(
            """
            public abstract class Shape
            {
                public abstract double Area { get; }
            }

            public abstract class Rounded : Shape
            {
                public double Radius { get; set; }
            }
            """);

    /// <summary>Verifies a type whose base is abstract but fully implemented by the chain is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Once the last abstract member has an override, nothing is left for a derived type to supply.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImplementedInheritedAbstractMemberIsReportedAsync() =>
        VerifyAbstractType.VerifyAnalyzerAsync(
            """
            public abstract class Shape
            {
                public abstract double Area { get; }
            }

            public abstract class {|SST1496:Square|} : Shape
            {
                public double Side { get; set; }

                public override double Area => Side * Side;
            }
            """);

    /// <summary>Verifies an abstract member declared in another part of a partial type counts.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PartialTypeIsJudgedFromAllPartsAsync() =>
        VerifyAbstractType.VerifyAnalyzerAsync(
            """
            public abstract partial class Handler
            {
                public string Name { get; set; }
            }

            public abstract partial class Handler
            {
                public abstract void Handle();
            }
            """);

    /// <summary>Verifies a partial type with nothing abstract is reported once, and its fix is withheld.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The modifiers live in parts the fix cannot see all of, so the diagnostic stands without an edit.</remarks>
    [Test]
    public async Task PartialTypeIsReportedOnceAndNotFixedAsync()
    {
        const string Source = """
                              public abstract partial class {|SST1496:Handler|}
                              {
                                  public string Name { get; set; }
                              }

                              public abstract partial class Handler
                              {
                                  public void Handle()
                                  {
                                  }
                              }
                              """;
        await VerifyAbstractType.VerifyCodeFixAsync(Source, Source);
    }

    /// <summary>Verifies a static class is not reported, even though it is abstract in metadata.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticClassIsCleanAsync() =>
        VerifyAbstractType.VerifyAnalyzerAsync(
            """
            public static class Helpers
            {
                public static int Twice(int value) => value * 2;
            }
            """);

    /// <summary>Verifies an abstract record base is how a closed hierarchy is spelled, and is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AbstractRecordIsCleanAsync() =>
        VerifyAbstractType.VerifyAnalyzerAsync(
            """
            public abstract record Result;

            public sealed record Success(int Value) : Result;

            namespace System.Runtime.CompilerServices
            {
                internal static class IsExternalInit
                {
                }
            }
            """);

    /// <summary>Verifies a class something already derives from loses the seal but still drops 'abstract'.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DerivedFromClassOnlyDropsAbstractAsync()
    {
        const string Source = """
                              public abstract class {|SST1496:Node|}
                              {
                                  public int Depth { get; set; }
                              }

                              public sealed class Leaf : Node
                              {
                              }
                              """;
        const string FixedSource = """
                                   public class Node
                                   {
                                       public int Depth { get; set; }
                                   }

                                   public sealed class Leaf : Node
                                   {
                                   }
                                   """;
        await VerifyAbstractType.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a class written to be extended keeps its virtual and protected members and is not sealed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassWithInheritanceOnlyMembersIsNotSealedAsync()
    {
        const string Source = """
                              public abstract class {|SST1496:Template|}
                              {
                                  protected Template()
                                  {
                                  }

                                  public virtual void Run()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class Template
                                   {
                                       protected Template()
                                       {
                                       }

                                       public virtual void Run()
                                       {
                                       }
                                   }
                                   """;
        await VerifyAbstractType.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix keeps the documentation and the attributes attached to the declaration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixKeepsDocumentationWhenAbstractComesFirstAsync()
    {
        const string Source = """
                              /// <summary>A base with no contract.</summary>
                              abstract class {|SST1496:Bare|}
                              {
                                  public int Value { get; set; }
                              }
                              """;
        const string FixedSource = """
                                   /// <summary>A base with no contract.</summary>
                                   sealed class Bare
                                   {
                                       public int Value { get; set; }
                                   }
                                   """;
        await VerifyAbstractType.VerifyCodeFixAsync(Source, FixedSource);
    }
}
