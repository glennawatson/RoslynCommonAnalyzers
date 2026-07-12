// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;
using VerifyConstructors = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExceptionConstructorAnalyzer,
    StyleSharp.Analyzers.Sst1488ExceptionStandardConstructorsCodeFixProvider>;
using VerifySerialization = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.ExceptionConstructorAnalyzer,
    StyleSharp.Analyzers.Sst1489ObsoleteSerializationMemberCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for SST1488 (exception types should declare the standard constructors) and SST1489
/// (exception types should not carry formatter-based serialization members).
/// </summary>
public class ExceptionConstructorAnalyzerUnitTest
{
    /// <summary>The three constructors every exception is expected to declare.</summary>
    private const string StandardConstructors = """
                                                    /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                                    public WidgetException()
                                                    {
                                                    }

                                                    /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                                    /// <param name="message">The message that describes the error.</param>
                                                    public WidgetException(string message)
                                                        : base(message)
                                                    {
                                                    }

                                                    /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                                    /// <param name="innerException">The exception that is the cause of this exception.</param>
                                                    /// <param name="message">The message that describes the error.</param>
                                                    public WidgetException(string message, System.Exception innerException)
                                                        : base(message, innerException)
                                                    {
                                                    }
                                                """;

    /// <summary>Verifies an exception declaring all three constructors is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExceptionWithAllConstructorsIsCleanAsync()
        => await VerifyAsync($$"""
            public class WidgetException : System.Exception
            {
            {{StandardConstructors}}
            }
            """);

    /// <summary>Verifies an exception with no constructors at all is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExceptionWithNoConstructorsIsReportedAsync()
        => await VerifyAsync("""
            public class {|SST1488:WidgetException|} : System.Exception
            {
            }
            """);

    /// <summary>Verifies an exception that cannot wrap a cause is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExceptionWithoutInnerExceptionConstructorIsReportedAsync()
        => await VerifyAsync("""
            public class {|SST1488:WidgetException|} : System.Exception
            {
                /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                public WidgetException()
                {
                }

                /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                /// <param name="message">The message that describes the error.</param>
                public WidgetException(string message)
                    : base(message)
                {
                }
            }
            """);

    /// <summary>Verifies a type deriving from a custom exception is measured on its own constructors.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>A derived type does not inherit its base's constructors, so it must declare its own.</remarks>
    [Test]
    public async Task DerivedExceptionDoesNotInheritConstructorsAsync()
        => await VerifyAsync($$"""
            public class WidgetException : System.Exception
            {
            {{StandardConstructors}}
            }

            public class {|SST1488:GadgetException|} : WidgetException
            {
            }
            """);

    /// <summary>Verifies a non-exception type is never measured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonExceptionTypeIsCleanAsync()
        => await VerifyAsync("""
            public class Widget
            {
            }
            """);

    /// <summary>Verifies an abstract exception may declare its constructors as protected.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AbstractExceptionWithProtectedConstructorsIsCleanAsync()
        => await VerifyAsync("""
            public abstract class WidgetException : System.Exception
            {
                /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                protected WidgetException()
                {
                }

                /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                /// <param name="message">The message that describes the error.</param>
                protected WidgetException(string message)
                    : base(message)
                {
                }

                /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                /// <param name="innerException">The exception that is the cause of this exception.</param>
                /// <param name="message">The message that describes the error.</param>
                protected WidgetException(string message, System.Exception innerException)
                    : base(message, innerException)
                {
                }
            }
            """);

    /// <summary>Verifies a concrete exception whose constructors are private cannot be constructed and is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConcreteExceptionWithPrivateConstructorsIsReportedAsync()
        => await VerifyAsync("""
            public class {|SST1488:WidgetException|} : System.Exception
            {
                private WidgetException(string message)
                    : base(message)
                {
                }
            }
            """);

    /// <summary>Verifies the constructors are matched by parameter type, not by parameter name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstructorsAreMatchedByTypeNotNameAsync()
        => await VerifyAsync("""
            public class WidgetException : System.Exception
            {
                /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                public WidgetException()
                {
                }

                /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                /// <param name="reason">The message that describes the error.</param>
                public WidgetException(string reason)
                    : base(reason)
                {
                }

                /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                /// <param name="cause">The exception that is the cause of this exception.</param>
                /// <param name="reason">The message that describes the error.</param>
                public WidgetException(string reason, System.Exception cause)
                    : base(reason, cause)
                {
                }
            }
            """);

    /// <summary>Verifies the parameterless constructor can be waived by configuration.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ParameterlessConstructorCanBeWaivedAsync()
        => await VerifyWithConfigAsync(
            """
            public class WidgetException : System.Exception
            {
                /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                /// <param name="message">The message that describes the error.</param>
                public WidgetException(string message)
                    : base(message)
                {
                }

                /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                /// <param name="innerException">The exception that is the cause of this exception.</param>
                /// <param name="message">The message that describes the error.</param>
                public WidgetException(string message, System.Exception innerException)
                    : base(message, innerException)
                {
                }
            }
            """,
            "stylesharp.SST1488.require_parameterless = false");

    /// <summary>Verifies a non-public exception can be excluded by configuration.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonPublicExceptionCanBeExcludedAsync()
        => await VerifyWithConfigAsync(
            """
            internal class WidgetException : System.Exception
            {
            }
            """,
            "stylesharp.SST1488.include_non_public_types = false");

    /// <summary>Verifies a non-public exception is measured by default.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonPublicExceptionIsMeasuredByDefaultAsync()
        => await VerifyAsync("""
            internal class {|SST1488:WidgetException|} : System.Exception
            {
            }
            """);

    /// <summary>Verifies the fix adds every missing constructor, documented, forwarding to the base.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixAddsTheMissingConstructorsAsync()
    {
        const string Source = """
                              public class {|SST1488:WidgetException|} : System.Exception
                              {
                              }
                              """;
        const string FixedSource = """
                                   public class WidgetException : System.Exception
                                   {
                                       /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                       public WidgetException()
                                       {
                                       }

                                       /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                       /// <param name="message">The message that describes the error.</param>
                                       public WidgetException(string message)
                                           : base(message)
                                       {
                                       }

                                       /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                       /// <param name="message">The message that describes the error.</param>
                                       /// <param name="innerException">The exception that is the cause of this exception.</param>
                                       public WidgetException(string message, System.Exception innerException)
                                           : base(message, innerException)
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix declares protected constructors on an abstract exception.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixDeclaresProtectedConstructorsOnAnAbstractExceptionAsync()
    {
        const string Source = """
                              public abstract class {|SST1488:WidgetException|} : System.Exception
                              {
                              }
                              """;
        const string FixedSource = """
                                   public abstract class WidgetException : System.Exception
                                   {
                                       /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                       protected WidgetException()
                                       {
                                       }

                                       /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                       /// <param name="message">The message that describes the error.</param>
                                       protected WidgetException(string message)
                                           : base(message)
                                       {
                                       }

                                       /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                       /// <param name="message">The message that describes the error.</param>
                                       /// <param name="innerException">The exception that is the cause of this exception.</param>
                                       protected WidgetException(string message, System.Exception innerException)
                                           : base(message, innerException)
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the serialization constructor is reported on a framework that has obsoleted it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SerializationConstructorIsReportedOnAModernTargetAsync()
    {
        const string Source = """
                              using System.Runtime.Serialization;

                              public class WidgetException : System.Exception
                              {
                                  /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                  public WidgetException()
                                  {
                                  }

                                  /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                  /// <param name="message">The message that describes the error.</param>
                                  public WidgetException(string message)
                                      : base(message)
                                  {
                                  }

                                  /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                  /// <param name="innerException">The exception that is the cause of this exception.</param>
                                  /// <param name="message">The message that describes the error.</param>
                                  public WidgetException(string message, System.Exception innerException)
                                      : base(message, innerException)
                                  {
                                  }

                                  /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                  /// <param name="context">The streaming context.</param>
                                  /// <param name="info">The serialization payload.</param>
                                  protected {|SST1489:WidgetException|}(SerializationInfo info, StreamingContext context)
                                  {
                                  }
                              }
                              """;
        await VerifySerializationAsync(Source, ReferenceAssemblies.Net.Net80);
    }

    /// <summary>
    /// Verifies the serialization members are NOT reported on a framework that has not obsoleted them.
    /// This is the gate that keeps the rule off .NET Framework and netstandard2.0, where removing the
    /// members would break real serialization.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SerializationConstructorIsCleanOnAFrameworkThatStillUsesItAsync()
    {
        const string Source = """
                              using System.Runtime.Serialization;

                              public class WidgetException : System.Exception
                              {
                                  /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                  public WidgetException()
                                  {
                                  }

                                  /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                  /// <param name="message">The message that describes the error.</param>
                                  public WidgetException(string message)
                                      : base(message)
                                  {
                                  }

                                  /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                  /// <param name="innerException">The exception that is the cause of this exception.</param>
                                  /// <param name="message">The message that describes the error.</param>
                                  public WidgetException(string message, System.Exception innerException)
                                      : base(message, innerException)
                                  {
                                  }

                                  /// <summary>Initializes a new instance of the <see cref="WidgetException"/> class.</summary>
                                  /// <param name="context">The streaming context.</param>
                                  /// <param name="info">The serialization payload.</param>
                                  protected WidgetException(SerializationInfo info, StreamingContext context)
                                      : base(info, context)
                                  {
                                  }
                              }
                              """;
        await VerifySerializationAsync(Source, ReferenceAssemblies.NetStandard.NetStandard20);
    }

    /// <summary>Runs an analyzer-and-fix verification against a modern target.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source, when the fix is exercised.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new VerifyConstructors.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer verification with one editorconfig setting applied.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="setting">The editorconfig line to apply.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task VerifyWithConfigAsync(string source, string setting)
    {
        var test = new VerifyConstructors.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $"""
            root = true

            [*.cs]
            {setting}
            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the SST1489 verification against a chosen target framework.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="referenceAssemblies">The framework whose obsoletion state decides the outcome.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task VerifySerializationAsync(string source, ReferenceAssemblies referenceAssemblies)
    {
        var test = new VerifySerialization.Test
        {
            ReferenceAssemblies = referenceAssemblies,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
