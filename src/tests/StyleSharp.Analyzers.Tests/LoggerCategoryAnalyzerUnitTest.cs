// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyCategory = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2443LoggerCategoryAnalyzer,
    StyleSharp.Analyzers.Sst2443LoggerCategoryCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2443 (a typed logger categorized by the wrong type) and its fix.</summary>
public class LoggerCategoryAnalyzerUnitTest
{
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
    [Test]
    public async Task OwnTypeCategoryIsCleanAsync()
        => await VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class Bar
            {
                private ILogger<Bar> _logger;
            }
            """));

    /// <summary>Verifies a base type category is treated as deliberate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseTypeCategoryIsCleanAsync()
        => await VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public class Base { public virtual void Work() { } }

            public sealed class Bar : Base
            {
                private ILogger<Base> _logger;
            }
            """));

    /// <summary>Verifies an implemented interface category is treated as deliberate.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceCategoryIsCleanAsync()
        => await VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
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
    [Test]
    public async Task CategoryMarkerIsCleanAsync()
        => await VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class RequestCategory { public void Work() { } }

            public sealed class Bar
            {
                private ILogger<RequestCategory> _logger;
            }
            """));

    /// <summary>Verifies an empty marker type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyMarkerIsCleanAsync()
        => await VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class LoggingRoot { }

            public sealed class Bar
            {
                private ILogger<LoggingRoot> _logger;
            }
            """));

    /// <summary>Verifies a generic type naming its own constructed self is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericSelfCategoryIsCleanAsync()
        => await VerifyCategory.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class Repository<T>
            {
                private ILogger<Repository<T>> _logger;
            }
            """));
}
