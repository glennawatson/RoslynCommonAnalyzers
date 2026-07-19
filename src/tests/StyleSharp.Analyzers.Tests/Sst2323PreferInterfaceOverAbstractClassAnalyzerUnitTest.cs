// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2323PreferInterfaceOverAbstractClassAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2323 (an all-abstract class that would read better as an interface).</summary>
public class Sst2323PreferInterfaceOverAbstractClassAnalyzerUnitTest
{
    /// <summary>Verifies a class whose every member is abstract is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllAbstractClassIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class {|SST2323:Shape|}
            {
                public abstract double Area();

                public abstract void Draw();
            }
            """);

    /// <summary>Verifies an abstract property and event alongside methods still report.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractPropertyEventAndIndexerAreReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public abstract class {|SST2323:Surface|}
            {
                public abstract double Area { get; }

                public abstract event EventHandler Changed;

                public abstract int this[int index] { get; }

                public abstract void Draw();
            }
            """);

    /// <summary>Verifies a generic all-abstract class is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericAllAbstractClassIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class {|SST2323:Store|}<T>
            {
                public abstract T Get(int id);

                public abstract void Put(T value);
            }
            """);

    /// <summary>Verifies a partial class whose parts are all abstract is reported once.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialAllAbstractClassIsReportedOnceAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract partial class {|SST2323:Handler|}
            {
                public abstract void Open();
            }

            public abstract partial class Handler
            {
                public abstract void Close();
            }
            """);

    /// <summary>Verifies a nested class whose every member is abstract is itself reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The outer type holds a nested type, so only the all-abstract nested type is a contract candidate.</remarks>
    [Test]
    public async Task NestedAllAbstractClassIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class Outer
            {
                public abstract class {|SST2323:Formatter|}
                {
                    public abstract string Format(int value);
                }
            }
            """);

    /// <summary>Verifies a class with an instance field is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassWithFieldIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class Shape
            {
                private int _sides;

                public abstract double Area();
            }
            """);

    /// <summary>Verifies a class with an implemented method is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassWithImplementedMethodIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class Shape
            {
                public abstract double Area();

                public void Describe()
                {
                }
            }
            """);

    /// <summary>Verifies a class with a hand-written constructor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassWithConstructorIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class Shape
            {
                protected Shape()
                {
                }

                public abstract double Area();
            }
            """);

    /// <summary>Verifies an abstract class with a non-abstract property is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassWithNonAbstractMemberIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class Shape
            {
                public abstract double Area();

                public int Sides { get; set; }
            }
            """);

    /// <summary>Verifies an abstract member that is not public keeps the type silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassWithNonPublicAbstractMemberIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class Shape
            {
                protected abstract double Area();
            }
            """);

    /// <summary>Verifies an abstract class that declares nothing abstract is not reported here.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>That shape is the opposite predicate and belongs to the maintainability rule, not this one.</remarks>
    [Test]
    public async Task AbstractClassWithNoAbstractMemberIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class Shape
            {
                public void Describe()
                {
                }
            }
            """);

    /// <summary>Verifies an empty abstract class is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyAbstractClassIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class Marker
            {
            }
            """);

    /// <summary>Verifies an all-abstract class extending another class is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>An interface cannot inherit a class, so a real base class rules the conversion out.</remarks>
    [Test]
    public async Task AbstractClassWithBaseClassIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class Widget
            {
                public abstract void Render();

                public void Show()
                {
                }
            }

            public abstract class Sidebar : Widget
            {
                public abstract void Collapse();
            }
            """);

    /// <summary>Verifies a nested type keeps the outer class silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassWithNestedTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract class Shape
            {
                public abstract double Area();

                public sealed class Corner
                {
                    public int Angle { get; set; }
                }
            }
            """);

    /// <summary>Verifies a concrete class is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcreteClassIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class Shape
            {
                public double Area() => 0;
            }
            """);

    /// <summary>Verifies a static class is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticClassIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public static class Shapes
            {
                public static double Area() => 0;
            }
            """);

    /// <summary>Verifies an interface is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public interface IShape
            {
                double Area();

                void Draw();
            }
            """);

    /// <summary>Verifies an abstract record is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractRecordIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public abstract record Shape
            {
                public abstract double Area();
            }
            """);

    /// <summary>Verifies the rule stays silent below C# 8, where an interface cannot carry the members.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllAbstractClassOnOldLanguageVersionIsCleanAsync()
    {
        const string Source = """
                              public abstract class Shape
                              {
                                  public abstract double Area();

                                  public abstract void Draw();
                              }
                              """;
        var test = new Verify.Test { TestCode = Source };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp7_3));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
