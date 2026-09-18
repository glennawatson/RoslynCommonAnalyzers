// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Builds NUnit test signatures for startup, clean, and violating allocation profiles.</summary>
internal static class InvalidTestMethodShapeBenchmarkSource
{
    /// <summary>The estimated source length contributed by each test method.</summary>
    private const int CharactersPerMethod = 180;

    /// <summary>Creates the selected test-method corpus.</summary>
    /// <param name="nodes">The number of test methods.</param>
    /// <param name="scenario">The return-value contract being exercised.</param>
    /// <returns>A complete source file.</returns>
    internal static string Generate(int nodes, string scenario)
    {
        if (scenario == "Startup")
        {
            return "public class Empty { }";
        }

        var builder = new StringBuilder(nodes * CharactersPerMethod);
        _ = builder.AppendLine("""
            namespace NUnit.Framework
            {
                public class TestAttribute : System.Attribute { public object ExpectedResult { get; set; } }
                public class TheoryAttribute : System.Attribute { }
                public class ValuesAttribute : System.Attribute
                {
                    public ValuesAttribute(bool first, bool second) { }
                }
                public class TestCaseAttribute : System.Attribute
                {
                    public TestCaseAttribute(object value) { }
                    public object ExpectedResult { get; set; }
                }
                public class TestCaseSourceAttribute : System.Attribute
                {
                    public TestCaseSourceAttribute(string name) { }
                }
            }
            public class Tests
            {
                public static object[] Cases => new object[0];
            """);
        for (var i = 0; i < nodes; i++)
        {
            _ = builder.Append(scenario switch
            {
                "Ordinary" => "[NUnit.Framework.Test] public void Case",
                "ExpectedResult" => "[NUnit.Framework.TestCase(0, ExpectedResult = false)] public bool Case",
                "CaseSource" => "[NUnit.Framework.TestCaseSource(nameof(Cases))] public bool Case",
                "CombinedTest" => "[NUnit.Framework.Test(ExpectedResult = false)][NUnit.Framework.TestCase(0, ExpectedResult = false)] public bool Case",
                "Theory" => "[NUnit.Framework.Theory][NUnit.Framework.TestCase(false, ExpectedResult = false)] public bool Case",
                "Values" => "[NUnit.Framework.TestCase(false, ExpectedResult = false)] public bool Case",
                _ => "[NUnit.Framework.TestCase(0)] public bool Case",
            }).Append(i).AppendLine(scenario switch
            {
                "Ordinary" => "() { }",
                "Theory" => "(bool value) => false;",
                "Values" => "([NUnit.Framework.Values(false, true)] bool value) => false;",
                _ => "(int value) => false;",
            });
        }

        return builder.AppendLine("}").ToString();
    }
}
