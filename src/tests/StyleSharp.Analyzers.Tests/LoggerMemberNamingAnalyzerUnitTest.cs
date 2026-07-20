// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLogger = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2601LoggerMemberNamingAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2601 (logger field/property naming convention).</summary>
public class LoggerMemberNamingAnalyzerUnitTest
{
    /// <summary>Verifies a private instance logger field with a non-conventional name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateInstanceFieldWithWrongNameIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                private readonly ILogger {|SST2601:badName|} = default!;
            }
            """));

    /// <summary>Verifies a private instance logger field named <c>_logger</c> is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateInstanceFieldNamedUnderscoreLoggerIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                private readonly ILogger _logger = default!;
            }
            """));

    /// <summary>Verifies a private instance logger field named <c>_log</c> is clean under the default set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateInstanceFieldNamedUnderscoreLogIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                private readonly ILogger _log = default!;
            }
            """));

    /// <summary>Verifies a generic <c>ILogger&lt;T&gt;</c> field with a non-conventional name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericLoggerFieldWithWrongNameIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                private readonly ILogger<C> {|SST2601:log|} = default!;
            }
            """));

    /// <summary>Verifies a fully-qualified logger field with a non-conventional name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedLoggerFieldWithWrongNameIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                private readonly Microsoft.Extensions.Logging.ILogger {|SST2601:badName|} = default!;
            }
            """));

    /// <summary>Verifies a nullable logger field with a non-conventional name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableLoggerFieldWithWrongNameIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            #nullable enable
            public sealed class C
            {
                private readonly ILogger? {|SST2601:badName|};
            }
            """));

    /// <summary>Verifies a non-private logger property with a non-conventional name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicPropertyWithWrongNameIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public ILogger {|SST2601:MyLogger|} { get; } = default!;
            }
            """));

    /// <summary>Verifies a non-private logger property named <c>Logger</c> is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicPropertyNamedLoggerIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                public ILogger Logger { get; } = default!;
            }
            """));

    /// <summary>Verifies a non-private logger field named <c>_logger</c> is reported, since it should be <c>Logger</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonPrivateFieldNamedUnderscoreLoggerIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public class C
            {
                internal ILogger {|SST2601:_logger|} = default!;
            }
            """));

    /// <summary>Verifies a private static logger field named <c>_logger</c> is reported, since it should be <c>Logger</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticPrivateFieldNamedUnderscoreLoggerIsReportedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                private static readonly ILogger {|SST2601:_logger|} = default!;
            }
            """));

    /// <summary>Verifies only the mis-named declarator of a multi-declarator field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleDeclaratorsReportOnlyMisnamedAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                private ILogger _logger = default!, {|SST2601:other|} = default!;
            }
            """));

    /// <summary>Verifies the interface logger property is reported but its explicit implementation is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceLoggerPropertyIsReportedButExplicitImplementationIsNotAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public interface IHasLogger
            {
                ILogger {|SST2601:Tracer|} { get; }
            }

            public sealed class C : IHasLogger
            {
                ILogger IHasLogger.Tracer => default!;
            }
            """));

    /// <summary>Verifies a same-named type that is not the logger abstraction is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedSameNamedNonLoggerTypeIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                private readonly Other.ILogger badName = default!;
            }

            namespace Other
            {
                public interface ILogger
                {
                }
            }
            """));

    /// <summary>Verifies the rule stays silent when no logger abstraction is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoLoggerAbstractionAvailableIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(
            """
            public interface ILogger
            {
            }

            public sealed class C
            {
                private readonly ILogger _field = default!;
            }
            """);

    /// <summary>Verifies a type parameter named <c>ILogger</c> is not treated as the logger abstraction.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterNamedLoggerIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C<ILogger>
            {
                private readonly ILogger _field = default!;
            }
            """));

    /// <summary>Verifies a generic same-named type that does not derive from the logger abstraction is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericNonLoggerTypeIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                private readonly Other.ILogger<int> _field = default!;
            }

            namespace Other
            {
                public interface ILogger<T>
                {
                }
            }
            """));

    /// <summary>Verifies members whose type is not a logger are never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLoggerMembersAreCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public sealed class C
            {
                private readonly int _count = 0;
                private readonly C _self = default!;
                private readonly System.Collections.Generic.List<int> _items = new();

                public string Name { get; } = string.Empty;
            }
            """));

    /// <summary>Verifies a globally-aliased type that is not the logger abstraction is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GlobalAliasedNonLoggerTypeIsCleanAsync()
        => await VerifyLogger.VerifyAnalyzerAsync(LoggingTestSource.Wrap(
            """
            public interface ILogger
            {
            }

            public sealed class C
            {
                private readonly global::ILogger _field = default!;
            }
            """));

    /// <summary>Verifies the configured instance-field name replaces the default accepted set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfiguredInstanceFieldNameIsRespectedAsync()
    {
        var test = new VerifyLogger.Test
        {
            TestCode = LoggingTestSource.Wrap(
                """
                public sealed class C
                {
                    private readonly ILogger logger = default!;
                    private readonly ILogger {|SST2601:_logger|} = default!;
                }
                """)
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST2601.fieldname = logger

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an empty configured field name falls back to the default accepted set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyConfiguredFieldNameFallsBackToDefaultAsync()
    {
        var test = new VerifyLogger.Test
        {
            TestCode = LoggingTestSource.Wrap(
                """
                public sealed class C
                {
                    private readonly ILogger _logger = default!;
                    private readonly ILogger {|SST2601:badName|} = default!;
                }
                """)
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST2601.fieldname =

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a separator-only configured list still reports and suggests the built-in name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SeparatorOnlyConfiguredFieldNameStillReportsAsync()
    {
        var test = new VerifyLogger.Test
        {
            TestCode = LoggingTestSource.Wrap(
                """
                public sealed class C
                {
                    private readonly ILogger {|SST2601:badName|} = default!;
                }
                """)
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST2601.fieldname = ,

            """));

        await test.RunAsync(CancellationToken.None);
    }
}
