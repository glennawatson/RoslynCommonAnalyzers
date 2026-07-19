// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyAssert = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2503BooleanLiteralAssertionAnalyzer>;
using VerifyAssertFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2503BooleanLiteralAssertionAnalyzer,
    StyleSharp.Analyzers.Sst2503BooleanLiteralAssertionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2503 (a boolean literal handed to an equality assertion).</summary>
public class BooleanLiteralAssertionAnalyzerUnitTest
{
    /// <summary>Verifies an xUnit equality against <c>true</c> is reported and rewritten to the affirmative assertion.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitEqualTrueIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using Xunit;

                              public class C
                              {
                                  public void M(bool b)
                                  {
                                      Assert.{|SST2503:Equal|}(true, b);
                                  }
                              }

                              namespace Xunit
                              {
                                  public static class Assert
                                  {
                                      public static void Equal<T>(T expected, T actual) { }
                                      public static void True(bool condition) { }
                                      public static void False(bool condition) { }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using Xunit;

                                   public class C
                                   {
                                       public void M(bool b)
                                       {
                                           Assert.True(b);
                                       }
                                   }

                                   namespace Xunit
                                   {
                                       public static class Assert
                                       {
                                           public static void Equal<T>(T expected, T actual) { }
                                           public static void True(bool condition) { }
                                           public static void False(bool condition) { }
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an xUnit equality against <c>false</c> is reported and rewritten to the negative assertion.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitEqualFalseIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using Xunit;

                              public class C
                              {
                                  public void M(bool b)
                                  {
                                      Assert.{|SST2503:Equal|}(false, b);
                                  }
                              }

                              namespace Xunit
                              {
                                  public static class Assert
                                  {
                                      public static void Equal<T>(T expected, T actual) { }
                                      public static void True(bool condition) { }
                                      public static void False(bool condition) { }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using Xunit;

                                   public class C
                                   {
                                       public void M(bool b)
                                       {
                                           Assert.False(b);
                                       }
                                   }

                                   namespace Xunit
                                   {
                                       public static class Assert
                                       {
                                           public static void Equal<T>(T expected, T actual) { }
                                           public static void True(bool condition) { }
                                           public static void False(bool condition) { }
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an MSTest equality against <c>true</c> is reported and rewritten to <c>IsTrue</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MSTestAreEqualTrueIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using Microsoft.VisualStudio.TestTools.UnitTesting;

                              public class C
                              {
                                  public void M(bool b)
                                  {
                                      Assert.{|SST2503:AreEqual|}(true, b);
                                  }
                              }

                              namespace Microsoft.VisualStudio.TestTools.UnitTesting
                              {
                                  public static class Assert
                                  {
                                      public static void AreEqual<T>(T expected, T actual) { }
                                      public static void IsTrue(bool condition) { }
                                      public static void IsFalse(bool condition) { }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using Microsoft.VisualStudio.TestTools.UnitTesting;

                                   public class C
                                   {
                                       public void M(bool b)
                                       {
                                           Assert.IsTrue(b);
                                       }
                                   }

                                   namespace Microsoft.VisualStudio.TestTools.UnitTesting
                                   {
                                       public static class Assert
                                       {
                                           public static void AreEqual<T>(T expected, T actual) { }
                                           public static void IsTrue(bool condition) { }
                                           public static void IsFalse(bool condition) { }
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an MSTest equality against <c>false</c> is reported and rewritten to <c>IsFalse</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MSTestAreEqualFalseIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using Microsoft.VisualStudio.TestTools.UnitTesting;

                              public class C
                              {
                                  public void M(bool b)
                                  {
                                      Assert.{|SST2503:AreEqual|}(false, b);
                                  }
                              }

                              namespace Microsoft.VisualStudio.TestTools.UnitTesting
                              {
                                  public static class Assert
                                  {
                                      public static void AreEqual<T>(T expected, T actual) { }
                                      public static void IsTrue(bool condition) { }
                                      public static void IsFalse(bool condition) { }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using Microsoft.VisualStudio.TestTools.UnitTesting;

                                   public class C
                                   {
                                       public void M(bool b)
                                       {
                                           Assert.IsFalse(b);
                                       }
                                   }

                                   namespace Microsoft.VisualStudio.TestTools.UnitTesting
                                   {
                                       public static class Assert
                                       {
                                           public static void AreEqual<T>(T expected, T actual) { }
                                           public static void IsTrue(bool condition) { }
                                           public static void IsFalse(bool condition) { }
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an NUnit equality against <c>true</c> is reported and rewritten to <c>IsTrue</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitAreEqualTrueIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using NUnit.Framework;

                              public class C
                              {
                                  public void M(bool b)
                                  {
                                      Assert.{|SST2503:AreEqual|}(true, b);
                                  }
                              }

                              namespace NUnit.Framework
                              {
                                  public static class Assert
                                  {
                                      public static void AreEqual(object expected, object actual) { }
                                      public static void IsTrue(bool condition) { }
                                      public static void IsFalse(bool condition) { }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using NUnit.Framework;

                                   public class C
                                   {
                                       public void M(bool b)
                                       {
                                           Assert.IsTrue(b);
                                       }
                                   }

                                   namespace NUnit.Framework
                                   {
                                       public static class Assert
                                       {
                                           public static void AreEqual(object expected, object actual) { }
                                           public static void IsTrue(bool condition) { }
                                           public static void IsFalse(bool condition) { }
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the boolean literal is recognised in the actual (second) position too.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LiteralInActualPositionIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using Xunit;

                              public class C
                              {
                                  public void M(bool b)
                                  {
                                      Assert.{|SST2503:Equal|}(b, true);
                                  }
                              }

                              namespace Xunit
                              {
                                  public static class Assert
                                  {
                                      public static void Equal<T>(T expected, T actual) { }
                                      public static void True(bool condition) { }
                                      public static void False(bool condition) { }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using Xunit;

                                   public class C
                                   {
                                       public void M(bool b)
                                       {
                                           Assert.True(b);
                                       }
                                   }

                                   namespace Xunit
                                   {
                                       public static class Assert
                                       {
                                           public static void Equal<T>(T expected, T actual) { }
                                           public static void True(bool condition) { }
                                           public static void False(bool condition) { }
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a statically imported equality call is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticImportEqualIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using static Xunit.Assert;

                              public class C
                              {
                                  public void M(bool b)
                                  {
                                      {|SST2503:Equal|}(true, b);
                                  }
                              }

                              namespace Xunit
                              {
                                  public static class Assert
                                  {
                                      public static void Equal<T>(T expected, T actual) { }
                                      public static void True(bool condition) { }
                                      public static void False(bool condition) { }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using static Xunit.Assert;

                                   public class C
                                   {
                                       public void M(bool b)
                                       {
                                           True(b);
                                       }
                                   }

                                   namespace Xunit
                                   {
                                       public static class Assert
                                       {
                                           public static void Equal<T>(T expected, T actual) { }
                                           public static void True(bool condition) { }
                                           public static void False(bool condition) { }
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a boolean assertion whose only overload takes a params tail is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParamsBooleanAssertionIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using NUnit.Framework;

                              public class C
                              {
                                  public void M(bool b)
                                  {
                                      Assert.{|SST2503:AreEqual|}(true, b);
                                  }
                              }

                              namespace NUnit.Framework
                              {
                                  public static class Assert
                                  {
                                      public static void AreEqual(object expected, object actual) { }
                                      public static void IsTrue(bool condition, params object[] args) { }
                                      public static void IsFalse(bool condition, params object[] args) { }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using NUnit.Framework;

                                   public class C
                                   {
                                       public void M(bool b)
                                       {
                                           Assert.IsTrue(b);
                                       }
                                   }

                                   namespace NUnit.Framework
                                   {
                                       public static class Assert
                                       {
                                           public static void AreEqual(object expected, object actual) { }
                                           public static void IsTrue(bool condition, params object[] args) { }
                                           public static void IsFalse(bool condition, params object[] args) { }
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an equality against a non-boolean literal is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonBooleanLiteralExpectedIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                public void M(int count)
                {
                    Assert.AreEqual(3, count);
                }
            }

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class Assert
                {
                    public static void AreEqual<T>(T expected, T actual) { }
                    public static void IsTrue(bool condition) { }
                    public static void IsFalse(bool condition) { }
                }
            }
            """);

    /// <summary>Verifies an equality whose other operand is not a boolean value is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonBooleanActualIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                public void M(object o)
                {
                    Assert.AreEqual(true, o);
                }
            }

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class Assert
                {
                    public static void AreEqual<T>(T expected, T actual) { }
                    public static void IsTrue(bool condition) { }
                    public static void IsFalse(bool condition) { }
                }
            }
            """);

    /// <summary>Verifies a three-argument equality (with a message) is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThreeArgumentEqualityIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                public void M(bool b)
                {
                    Assert.AreEqual(true, b, "message");
                }
            }

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class Assert
                {
                    public static void AreEqual<T>(T expected, T actual, string message) { }
                    public static void IsTrue(bool condition) { }
                    public static void IsFalse(bool condition) { }
                }
            }
            """);

    /// <summary>Verifies an equality on an <c>Assert</c> class outside a recognised namespace is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrecognisedAssertNamespaceIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using Xunit;
            using Contoso;

            public class C
            {
                public void M(bool b)
                {
                    Contoso.Assert.AreEqual(true, b);
                }
            }

            namespace Xunit
            {
                public static class Assert
                {
                    public static void Equal<T>(T expected, T actual) { }
                    public static void True(bool condition) { }
                    public static void False(bool condition) { }
                }
            }

            namespace Contoso
            {
                public static class Assert
                {
                    public static void AreEqual<T>(T expected, T actual) { }
                    public static void IsTrue(bool condition) { }
                    public static void IsFalse(bool condition) { }
                }
            }
            """);

    /// <summary>Verifies a recognised equality name that is not the framework's equality method is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonClassicEqualityNameIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using NUnit.Framework;

            public class C
            {
                public void M(bool b)
                {
                    Assert.Equal(true, b);
                }
            }

            namespace NUnit.Framework
            {
                public static class Assert
                {
                    public static void Equal<T>(T expected, T actual) { }
                    public static void IsTrue(bool condition) { }
                    public static void IsFalse(bool condition) { }
                }
            }
            """);

    /// <summary>Verifies nothing is reported when the framework exposes no single-argument boolean assertion.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingBooleanAssertionIsCleanAsync()
        => await VerifyCleanAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                public void M(bool b)
                {
                    Assert.AreEqual(true, b);
                }
            }

            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public static class Assert
                {
                    public static void AreEqual<T>(T expected, T actual) { }
                    public static void IsTrue(bool condition, string message) { }
                    public static void IsFalse(bool condition, string message) { }
                }
            }
            """);

    /// <summary>Verifies a same-named call with no test framework referenced registers nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoTestFrameworkReferencedIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public void M(bool b)
                {
                    Helper.Equal(true, b);
                }
            }

            public static class Helper
            {
                public static void Equal<T>(T expected, T actual) { }
            }
            """);

    /// <summary>Runs a report-and-fix verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixAsync(string source, string fixedSource)
    {
        var test = new VerifyAssertFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source)
    {
        var test = new VerifyAssert.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
