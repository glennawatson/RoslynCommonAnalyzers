// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAbstractType = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1496AbstractTypeWithoutAbstractMembersAnalyzer,
    StyleSharp.Analyzers.Sst1496AbstractTypeWithoutAbstractMembersCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1496 (an abstract type declares nothing abstract) and its fix.</summary>
public class AbstractTypeWithoutAbstractMembersAnalyzerUnitTest
{
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
    [Test]
    public async Task AbstractMemberIsCleanAsync()
        => await VerifyAbstractType.VerifyAnalyzerAsync(
            """
            public abstract class Shape
            {
                public abstract double Area { get; }

                public override string ToString() => Area.ToString();
            }
            """);

    /// <summary>Verifies every kind of abstract member counts, not only methods.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryAbstractMemberKindCountsAsync()
        => await VerifyAbstractType.VerifyAnalyzerAsync(
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
    [Test]
    public async Task InheritedAbstractMemberIsCleanAsync()
        => await VerifyAbstractType.VerifyAnalyzerAsync(
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
    [Test]
    public async Task ImplementedInheritedAbstractMemberIsReportedAsync()
        => await VerifyAbstractType.VerifyAnalyzerAsync(
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
    [Test]
    public async Task PartialTypeIsJudgedFromAllPartsAsync()
        => await VerifyAbstractType.VerifyAnalyzerAsync(
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
    [Test]
    public async Task StaticClassIsCleanAsync()
        => await VerifyAbstractType.VerifyAnalyzerAsync(
            """
            public static class Helpers
            {
                public static int Twice(int value) => value * 2;
            }
            """);

    /// <summary>Verifies an abstract record base is how a closed hierarchy is spelled, and is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractRecordIsCleanAsync()
        => await VerifyAbstractType.VerifyAnalyzerAsync(
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
