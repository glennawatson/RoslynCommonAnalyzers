// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using VerifyCategory = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2443LoggerCategoryAnalyzer,
    StyleSharp.Analyzers.Sst2443LoggerCategoryCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2443 (a typed logger categorized by the wrong type) and its fix.</summary>
public class LoggerCategoryAnalyzerUnitTest
{
    /// <summary>Verifies top-level factories have no enclosing category to suggest.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TopLevelFactoryIsCleanAsync()
    {
        var test = new VerifyCategory.Test
        {
            TestCode = """
                new Factory().CreateLogger<Other>();
                class Other { public void Work() { } }
                class Factory
                {
                    public Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>() => null;
                }
                namespace Microsoft.Extensions.Logging { public interface ILogger<T> { } }
                """,
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
            solution.WithProjectCompilationOptions(projectId, solution.GetProject(projectId)!.CompilationOptions!.WithOutputKind(OutputKind.ConsoleApplication)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the typeof factory overload reports the category and accepts its own type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task TypeofFactoryAndPropertyCategoriesAsync() =>
        VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap("""
            class Other { public void Work() { } }
            class C
            {
                public ILogger<{|SST2443:Other|}> Logger { get; }
                void M(ILoggerFactory factory)
                {
                    M(factory);
                    System.GC.KeepAlive(factory);
                    factory.CreateLogger(typeof({|SST2443:Other|}));
                    factory.CreateLogger(typeof(C));
                    factory.CreateLogger("Category");
                }
            }
            """));

    /// <summary>Verifies nested categories, marker suffixes, and type parameters remain deliberate.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DeliberateCategoriesAreCleanAsync() =>
        VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap("""
            class RequestLogs { public void Work() { } }
            class Outer
            {
                class Middle
                {
                    class Inner<T>
                    {
                        public ILogger<Outer> OuterLogger;
                        public ILogger<RequestLogs> Marker;
                        public ILogger<T> Generic;
                        void M(ILoggerFactory factory)
                        {
                            factory.CreateLogger<T>();
                            factory.CreateLogger(typeof(T));
                        }
                    }
                }
            }
            """));

    /// <summary>Verifies unrelated interfaces and unrelated containing types do not grant exemptions.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnrelatedInterfaceCategoryIsReportedAsync() =>
        VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap("""
            interface IFirst { void Work(); }
            interface ISecond { void Work(); }
            interface IOther { void Work(); }
            class Outer
            {
                class C : IFirst, ISecond
                {
                    public void Work() { }
                    public ILogger<ISecond> Valid;
                    public ILogger<{|SST2443:IOther|}> Invalid;
                }
            }
            """));

    /// <summary>Verifies similarly named types and factories must bind to the logging abstraction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnrelatedLoggerShapesAreCleanAsync() =>
        VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap("""
            namespace Unrelated
            {
                interface ILogger<T> { }
                interface ILogger<T, U> { }
                class Other { public void Work() { } }
                class Factory
                {
                    public int CreateLogger<T>() => 0;
                    public ILogger<Other> CreateLogger(System.Type type) => null;
                }
                class C
                {
                    public ILogger<Other> Logger;
                    public ILogger<Other, C> Pair;
                    void M(Factory factory)
                    {
                        factory.CreateLogger<Other>();
                        factory.CreateLogger(typeof(Other));
                    }
                }
            }
            """));

    /// <summary>Verifies missing logging metadata disables matching generic and typeof factory calls.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MissingLoggingTypesAreCleanAsync() =>
        VerifyCategory.VerifyAnalyzerAsync("""
            interface ILogger<T> { }
            class Other { public void Work() { } }
            class C
            {
                public ILogger<Other> Logger;
                public ILogger<T> CreateLogger<T>() => null;
                public object CreateLogger(System.Type type) => null;
                void M() { this.CreateLogger<Other>(); this.CreateLogger(typeof(Other)); }
            }
            """);

    /// <summary>Verifies a field logger categorized by another type is corrected to the enclosing type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldCategoryIsCorrectedAsync()
    {
        var source = LoggingTestSource.Wrap(
            """
            public sealed class Foo { public void Work() { } }

            public sealed class Bar
            {
                private ILogger<{|SST2443:Foo|}> _logger;
            }
            """);
        var fixedSource = LoggingTestSource.Wrap(
            """
            public sealed class Foo { public void Work() { } }

            public sealed class Bar
            {
                private ILogger<Bar> _logger;
            }
            """);
        await VerifyCategory.VerifyCodeFixAsync(source, fixedSource);
    }

    /// <summary>Verifies a constructor parameter logger is corrected to the enclosing type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterCategoryIsCorrectedAsync()
    {
        var source = LoggingTestSource.Wrap(
            """
            public sealed class Foo { public void Work() { } }

            public sealed class Bar
            {
                public Bar(ILogger<{|SST2443:Foo|}> logger) { }
            }
            """);
        var fixedSource = LoggingTestSource.Wrap(
            """
            public sealed class Foo { public void Work() { } }

            public sealed class Bar
            {
                public Bar(ILogger<Bar> logger) { }
            }
            """);
        await VerifyCategory.VerifyCodeFixAsync(source, fixedSource);
    }

    /// <summary>Verifies a factory call categorized by another type is corrected.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreateLoggerCategoryIsCorrectedAsync()
    {
        var source = LoggingTestSource.Wrap(
            """
            public sealed class Foo { public void Work() { } }

            public sealed class Bar
            {
                public ILogger Build(ILoggerFactory factory) => factory.CreateLogger<{|SST2443:Foo|}>();
            }
            """);
        var fixedSource = LoggingTestSource.Wrap(
            """
            public sealed class Foo { public void Work() { } }

            public sealed class Bar
            {
                public ILogger Build(ILoggerFactory factory) => factory.CreateLogger<Bar>();
            }
            """);
        await VerifyCategory.VerifyCodeFixAsync(source, fixedSource);
    }

    /// <summary>Verifies a logger categorized by its own type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OwnTypeCategoryIsCleanAsync() =>
        VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class Bar
            {
                private ILogger<Bar> _logger;
            }
            """));

    /// <summary>Verifies a base type category is treated as deliberate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BaseTypeCategoryIsCleanAsync() =>
        VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public class Base { public virtual void Work() { } }

            public sealed class Bar : Base
            {
                private ILogger<Base> _logger;
            }
            """));

    /// <summary>Verifies an implemented interface category is treated as deliberate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InterfaceCategoryIsCleanAsync() =>
        VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public interface IThing { void Work(); }

            public sealed class Bar : IThing
            {
                public void Work() { }

                private ILogger<IThing> _logger;
            }
            """));

    /// <summary>Verifies a dedicated category marker type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CategoryMarkerIsCleanAsync() =>
        VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class RequestCategory { public void Work() { } }

            public sealed class Bar
            {
                private ILogger<RequestCategory> _logger;
            }
            """));

    /// <summary>Verifies an empty marker type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmptyMarkerIsCleanAsync() =>
        VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class LoggingRoot { }

            public sealed class Bar
            {
                private ILogger<LoggingRoot> _logger;
            }
            """));

    /// <summary>Verifies a generic type naming its own constructed self is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GenericSelfCategoryIsCleanAsync() =>
        VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class Repository<T>
            {
                private ILogger<Repository<T>> _logger;
            }
            """));
}
