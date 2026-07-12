// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyExceptionNeverThrown = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1480ExceptionNeverThrownAnalyzer,
    StyleSharp.Analyzers.Sst1480ExceptionNeverThrownCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1480 (a constructed exception should be thrown) and its fix.</summary>
public class ExceptionNeverThrownAnalyzerUnitTest
{
    /// <summary>Verifies the forgotten throw — an exception constructed as a whole statement — is reported and restored.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardedExceptionIsThrownAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public void M(string value)
                                  {
                                      if (value is null)
                                      {
                                          {|SST1480:new ArgumentNullException(nameof(value))|};
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public void M(string value)
                                       {
                                           if (value is null)
                                           {
                                               throw new ArgumentNullException(nameof(value));
                                           }
                                       }
                                   }
                                   """;
        await VerifyExceptionNeverThrown.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an exception several levels down its own hierarchy is still recognized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DerivedExceptionIsThrownAsync()
    {
        const string Source = """
                              using System;

                              public class DomainException : Exception
                              {
                              }

                              public sealed class OrderException : DomainException
                              {
                              }

                              public sealed class C
                              {
                                  public void M()
                                  {
                                      {|SST1480:new OrderException()|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class DomainException : Exception
                                   {
                                   }

                                   public sealed class OrderException : DomainException
                                   {
                                   }

                                   public sealed class C
                                   {
                                       public void M()
                                       {
                                           throw new OrderException();
                                       }
                                   }
                                   """;
        await VerifyExceptionNeverThrown.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an exception that is thrown is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrownExceptionIsCleanAsync()
        => await VerifyExceptionNeverThrown.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(int value)
                {
                    if (value < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }
                }
            }
            """);

    /// <summary>Verifies an exception factory that returns its exception is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnedExceptionIsCleanAsync()
        => await VerifyExceptionNeverThrown.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public Exception Expression() => new InvalidOperationException("no");

                public Exception Statement()
                {
                    return new InvalidOperationException("no");
                }
            }
            """);

    /// <summary>Verifies an exception assigned to a local, a field or a property is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignedExceptionIsCleanAsync()
        => await VerifyExceptionNeverThrown.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private Exception _last = new InvalidOperationException("field");

                public Exception Last { get; private set; } = new InvalidOperationException("property");

                public Exception Build()
                {
                    var error = new InvalidOperationException("local");
                    _last = new InvalidOperationException("assigned");
                    Last = new InvalidOperationException("set");
                    return error;
                }
            }
            """);

    /// <summary>Verifies an exception passed as an argument is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentExceptionIsCleanAsync()
        => await VerifyExceptionNeverThrown.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public void M(TaskCompletionSource<int> source)
                {
                    Fail(new InvalidOperationException("argument"));
                    source.SetException(new InvalidOperationException("task"));
                }

                private static void Fail(Exception error)
                {
                }
            }
            """);

    /// <summary>Verifies an exception produced by a lambda or captured in a collection is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapturedExceptionIsCleanAsync()
        => await VerifyExceptionNeverThrown.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public sealed class C
            {
                public Func<Exception> Factory() => () => new InvalidOperationException("lambda");

                public List<Exception> Collected() => new List<Exception> { new InvalidOperationException("initializer") };

                public Exception[] Many() => new Exception[] { new InvalidOperationException("array") };
            }
            """);

    /// <summary>Verifies a discarded assignment is not the shape this rule reports.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The creation's parent is the assignment, not the statement, and a discard is a deliberate act — the
    /// author wrote something on the left of the exception, which is not the mistake this rule is about.
    /// </remarks>
    [Test]
    public async Task DiscardAssignmentIsCleanAsync()
        => await VerifyExceptionNeverThrown.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M()
                {
                    _ = new InvalidOperationException("discarded");
                }
            }
            """);

    /// <summary>Verifies an object that is not an exception may be constructed as a statement.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The bind is what makes this clean, and it is the only thing the rule uses the semantic model for.</remarks>
    [Test]
    public async Task NonExceptionCreationIsCleanAsync()
        => await VerifyExceptionNeverThrown.VerifyAnalyzerAsync(
            """
            using System.Text;

            public sealed class C
            {
                public void M()
                {
                    new object();
                    new StringBuilder();
                }
            }
            """);

    /// <summary>Verifies the implicit <c>new(...)</c> form is understood wherever its value is consumed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A target-typed creation cannot stand alone as a statement — an expression statement gives it no target
    /// type — so the implicit form only ever appears in a consuming position, and is always clean.
    /// </remarks>
    [Test]
    public async Task ImplicitCreationIsCleanAsync()
        => await VerifyExceptionNeverThrown.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public InvalidOperationException Factory() => new("expression");

                public Exception Build()
                {
                    InvalidOperationException error = new("local");
                    return error;
                }
            }
            """);

    /// <summary>Verifies the document-based Fix All restores every forgotten throw in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllThrowsEveryOccurrenceAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public void First(string value)
                                  {
                                      if (value is null)
                                      {
                                          {|SST1480:new ArgumentNullException(nameof(value))|};
                                      }
                                  }

                                  public void Second(int value)
                                  {
                                      if (value < 0)
                                      {
                                          {|SST1480:new ArgumentOutOfRangeException(nameof(value))|};
                                      }
                                  }

                                  public void Third()
                                  {
                                      {|SST1480:new InvalidOperationException("unreachable")|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public void First(string value)
                                       {
                                           if (value is null)
                                           {
                                               throw new ArgumentNullException(nameof(value));
                                           }
                                       }

                                       public void Second(int value)
                                       {
                                           if (value < 0)
                                           {
                                               throw new ArgumentOutOfRangeException(nameof(value));
                                           }
                                       }

                                       public void Third()
                                       {
                                           throw new InvalidOperationException("unreachable");
                                       }
                                   }
                                   """;
        await VerifyExceptionNeverThrown.VerifyCodeFixAsync(Source, FixedSource);
    }
}
