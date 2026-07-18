// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyShadowed = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1484ShadowedDeclarationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1484 (declarations should not shadow an outer field or property).</summary>
public class ShadowedDeclarationAnalyzerUnitTest
{
    /// <summary>The <c>init</c>-accessor polyfill positional records require on the test reference assemblies.</summary>
    private const string IsExternalInit = """

        namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
        """;

    /// <summary>Verifies a local that reuses a field's name is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LocalShadowingFieldIsReportedAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int count;

                public int Read() => count;

                public int Measure(int[] values)
                {
                    var {|SST1484:count|} = values.Length;
                    return count;
                }
            }
            """);

    /// <summary>Verifies a local that reuses a property's name is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LocalShadowingPropertyIsReportedAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Total { get; set; }

                public int Measure(int[] values)
                {
                    var {|SST1484:Total|} = values.Length;
                    return Total;
                }
            }
            """);

    /// <summary>Verifies a method parameter that reuses a field's name without storing it is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ParameterShadowingFieldIsReportedAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class C
            {
                private string name;

                public string Get() => name;

                public int Measure(string {|SST1484:name|}) => name.Length;
            }
            """);

    /// <summary>Verifies the classic constructor idiom stays clean in every spelling it is written in.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This is the false positive the rule cannot afford. A constructor parameter that is assigned to the
    /// field it shadows is how C# construction is written, so every shape of it — block-bodied,
    /// expression-bodied, unqualified, tuple, and forwarded to another constructor — has to stay silent.
    /// </remarks>
    [Test]
    public async Task ConstructorAssignmentIdiomIsCleanAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class Classic
            {
                private readonly string name;

                public Classic(string name)
                {
                    this.name = name;
                }

                public string Describe() => name;
            }

            public class Bare
            {
                private readonly string name;

                public Bare(string name)
                {
                    name = name;
                }

                public string Describe() => name;
            }

            public class ExpressionBodied
            {
                private readonly string name;

                public ExpressionBodied(string name) => this.name = name;

                public string Describe() => name;
            }

            public class Tuples
            {
                private readonly string name;
                private readonly int age;

                public Tuples(string name, int age) => (this.name, this.age) = (name, age);

                public string Describe() => name + age;
            }

            public class Chained
            {
                private readonly string name;

                public Chained(string name)
                    : this(name, 0)
                {
                }

                public Chained(string name, int age)
                {
                    this.name = name;
                    Age = age;
                }

                public int Age { get; }

                public string Describe() => name;
            }

            public class Guarded
            {
                private readonly string name;

                public Guarded(string name)
                {
                    this.name = name ?? "unknown";
                }

                public string Describe() => name;
            }
            """);

    /// <summary>Verifies a primary constructor's parameters and a positional record's are never reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The language scopes a primary constructor's parameters over the whole type body on purpose, and
    /// backing a member is the reason they exist. A positional record's properties are generated from the
    /// very parameters that would be reported.
    /// </remarks>
    [Test]
    public async Task PrimaryConstructorParametersAreCleanAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            $$"""
            public record Person(string Name, int Age);

            public record struct Point(int X, int Y);

            public class Service(string logger)
            {
                private readonly string _logger = logger;

                public string Describe() => _logger;
            }{{IsExternalInit}}
            """);

    /// <summary>Verifies a setter-shaped method that stores its argument is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ParameterThatFeedsTheMemberIsCleanAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class C
            {
                private string name;

                public void SetName(string name) => this.name = name;

                public string Get() => name;
            }
            """);

    /// <summary>Verifies an instance field is not in scope inside a static member, so a local cannot shadow it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StaticMemberDoesNotSeeInstanceFieldsAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int count;
                private static int total;

                public int Read() => count + total;

                public static int Measure(int[] values)
                {
                    var count = values.Length;
                    return count;
                }

                public static int Sum(int[] values)
                {
                    var {|SST1484:total|} = values.Length;
                    return total;
                }
            }
            """);

    /// <summary>Verifies a static local function does not see the instance fields of its enclosing method.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StaticLocalFunctionDoesNotSeeInstanceFieldsAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int count;

                public int Read() => count;

                public int Measure(int[] values)
                {
                    static int Local(int[] items)
                    {
                        var count = items.Length;
                        return count;
                    }

                    return Local(values);
                }
            }
            """);

    /// <summary>Verifies a loop variable, a pattern variable, an out variable and a catch variable are all measured.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EveryLocalDeclarationFormIsMeasuredAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int count;
                private string text;
                private object error;

                public string Read() => text + count + error;

                public bool Parse(string input)
                {
                    if (int.TryParse(input, out var {|SST1484:count|}))
                    {
                        return count > 0;
                    }

                    return false;
                }

                public bool Match(object value)
                {
                    if (value is string {|SST1484:text|})
                    {
                        return text.Length > 0;
                    }

                    return false;
                }

                public int Longest(string[] values)
                {
                    var longest = 0;
                    foreach (var {|SST1484:text|} in values)
                    {
                        longest += text.Length;
                    }

                    return longest;
                }

                public void Handle()
                {
                    try
                    {
                        Work();
                    }
                    catch (System.InvalidOperationException {|SST1484:error|})
                    {
                        Log(error.Message);
                    }
                }

                private static void Work()
                {
                }

                private static void Log(string message)
                {
                }
            }
            """);

    /// <summary>Verifies a local that shadows a visible base-type field is reported, and a private one is not.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InheritedMembersAreShadowedOnlyWhenVisibleAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class Base
            {
                protected int shared;
                private int secret;

                public int Read() => secret;
            }

            public class Derived : Base
            {
                public int Visible(int[] values)
                {
                    var {|SST1484:shared|} = values.Length;
                    return shared;
                }

                public int Invisible(int[] values)
                {
                    var secret = values.Length;
                    return secret;
                }
            }
            """);

    /// <summary>Verifies a name that only matches a member of an unrelated type is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnrelatedTypeMembersAreCleanAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public static class Defaults
            {
                public static int Limit;
            }

            public class C
            {
                public int Measure(int[] values)
                {
                    var Limit = values.Length;
                    return Limit;
                }
            }
            """);

    /// <summary>Verifies a nested type's field that reuses a containing type's static member name is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Inside the nested type the simple name resolves to the nested member, so a reader who knows the
    /// containing type's static member resolves the wrong symbol. A static field, a const and a static
    /// property are all in scope by simple name and all count.
    /// </remarks>
    [Test]
    public async Task NestedTypeFieldShadowingOuterStaticMemberIsReportedAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class Registry
            {
                public static int Count;
                public const int Max = 10;
                public static int Version { get; set; }

                public int Read() => Count + Max + Version;

                public class Entry
                {
                    private int {|SST1484:Count|};
                    private const int {|SST1484:Max|} = 20;
                    private int {|SST1484:Version|};

                    public int Read() => Count + Max + Version;
                }
            }
            """);

    /// <summary>Verifies a nested type's property that reuses a containing type's static member name is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NestedTypePropertyShadowingOuterStaticMemberIsReportedAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class Configuration
            {
                public static string Owner { get; set; }
                public static int Limit;

                public string Read() => Owner + Limit;

                public class Section
                {
                    public string {|SST1484:Owner|} { get; set; }

                    public int {|SST1484:Limit|} { get; set; }
                }
            }
            """);

    /// <summary>Verifies every containing type is measured, including statics the containing type inherits.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NestedTypeMemberShadowingDeeperOuterStaticIsReportedAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class BaseSettings
            {
                protected static int Threshold;
            }

            public class Settings : BaseSettings
            {
                public static string Owner;

                public string Read() => Owner + Threshold;

                public class Group
                {
                    public class Item
                    {
                        private string {|SST1484:Owner|};
                        private int {|SST1484:Threshold|};

                        public string Read() => Owner + Threshold;
                    }
                }
            }
            """);

    /// <summary>Verifies a containing type's instance member cannot be shadowed by a nested type's member.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// An outer instance member is not reachable by simple name from a nested type, so nothing is ambiguous.
    /// The walk also stops at the nearest containing type that claims the name, the way C# resolves it:
    /// <c>Leaf.Value</c> is clean because <c>Middle</c>'s claim on the name is an instance field, even though
    /// a farther containing type has a static of that name — which is why <c>Middle.Value</c> itself is the
    /// one reported.
    /// </remarks>
    [Test]
    public async Task NestedTypeMemberMatchingOuterInstanceMemberIsCleanAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class Host
            {
                private int count;

                public int Read() => count;

                public class Worker
                {
                    private int count;

                    public int Read() => count;
                }
            }

            public class Root
            {
                public static int Value { get; set; }

                public class Middle
                {
                    private int {|SST1484:Value|};

                    public int Read() => Value;

                    public class Leaf
                    {
                        private int Value;

                        public int Read() => Value;
                    }
                }
            }
            """);

    /// <summary>Verifies a name a nested member did not choose for itself is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// An <c>override</c> keeps the name its base declared, a member marked <c>new</c> says the hiding is
    /// deliberate, and an explicit interface implementation is not reachable by simple name at all.
    /// </remarks>
    [Test]
    public async Task NestedTypeMemberWithContractOrDeliberateNameIsCleanAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public interface INamed
            {
                string Name { get; }
            }

            public class BaseEntry
            {
                public virtual string Title { get; set; }

                protected string label;

                public string Read() => Title + label;
            }

            public class Panel
            {
                public static string Title { get; set; }

                public static string Name;

                public static string label;

                public string Read() => Title + Name + label;

                public class Entry : BaseEntry
                {
                    public override string Title { get; set; }

                    private new string label;

                    public string Get() => label;
                }

                public class Card : INamed
                {
                    string INamed.Name => "card";
                }
            }
            """);

    /// <summary>Verifies a sibling nested type's static member is not in scope and so cannot be shadowed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SiblingNestedTypeMembersAreCleanAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class Pair
            {
                public class First
                {
                    public static int Size;

                    public int Read() => Size;
                }

                public class Second
                {
                    private int Size;

                    public int Read() => Size;
                }
            }
            """);

    /// <summary>Verifies a discard-named member and a catch clause with no name shadow nothing.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DiscardNamedMemberAndNamelessCatchAreCleanAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class Shell
            {
                public static int _;

                public int Read() => _;

                public void Handle()
                {
                    try
                    {
                        Read();
                    }
                    catch (System.InvalidOperationException)
                    {
                    }
                }

                public class Inner
                {
                    public int _ { get; set; }
                }
            }
            """);

    /// <summary>Verifies a field that hides an inherited field is not reported by default.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FieldHidingInheritedFieldIsCleanByDefaultAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class Base
            {
                protected int count;
            }

            public class Derived : Base
            {
                private int count;

                public int Read() => count;
            }
            """);

    /// <summary>Verifies a field that hides an inherited field is reported once the base-type check is on.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FieldHidingInheritedFieldIsReportedWhenOptedInAsync()
    {
        var test = new VerifyShadowed.Test
        {
            TestCode = """
                       public class Base
                       {
                           protected int count;
                       }

                       public class Derived : Base
                       {
                           private int {|SST1484:count|};

                           public int Read() => count;
                       }

                       public class Deliberate : Base
                       {
                           private new int count;

                           public int Read() => count;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1484.check_base_types = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the project-wide key applies when no rule-specific key is set.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GeneralBaseTypeKeyAppliesAsync()
    {
        var test = new VerifyShadowed.Test
        {
            TestCode = """
                       public class Base
                       {
                           protected int count;
                       }

                       public class Derived : Base
                       {
                           private int {|SST1484:count|};

                           public int Read() => count;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.check_base_types = true

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an unparsable option leaves the base-type check off rather than switching it on.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnparsableOptionFallsBackToTheDefaultAsync()
    {
        var test = new VerifyShadowed.Test
        {
            TestCode = """
                       public class Base
                       {
                           protected int count;
                       }

                       public class Derived : Base
                       {
                           private int count;

                           public int Read() => count;
                       }
                       """,
        };

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST1484.check_base_types = yes please

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a discard names nothing and so shadows nothing.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DiscardIsCleanAsync()
        => await VerifyShadowed.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _;

                public int Read() => _;

                public bool Parse(string input)
                {
                    return int.TryParse(input, out var _);
                }
            }
            """);
}
