// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Builds C# source-code strings used as fixtures in analyzer unit tests.</summary>
internal sealed class ClassGeneratorBuilder
{
    /// <summary>The number of leading spaces indenting a staggered parameter or argument line nested inside a method body.</summary>
    private const int NestedBodyIndentSpaces = 16;

    /// <summary>The number of lines a jagged parameter or argument list is split across.</summary>
    private const int JaggedLineCount = 2;

    /// <summary>Opens a generated method declaration, up to the parameter list that follows it.</summary>
    private const string MethodDeclarationOpening = """

                public void MyMethod(
        """;

    /// <summary>Opens a generated constructor declaration, up to the parameter list that follows it.</summary>
    private const string ConstructorDeclarationOpening = """

                public MyTestClass(
        """;

    /// <summary>Opens a generated delegate declaration, up to the parameter list that follows it.</summary>
    private const string DelegateDeclarationOpening = """

                public delegate void DelegateDefinition(
        """;

    /// <summary>Opens the nested type whose constructor is the object-creation target, up to its parameter list.</summary>
    private const string NestedTypeWithConstructorOpening = """

                public class MyInnerTest
                {
                    public MyInnerTest(
        """;

    /// <summary>Opens the nested attribute type whose constructor the attribute usage targets, up to its parameter list.</summary>
    private const string AttributeTypeWithConstructorOpening = """

                public class MyInnerTestAttribute : System.Attribute
                {
                    public MyInnerTestAttribute(
        """;

    /// <summary>Closes a parameter list and gives the member an empty body.</summary>
    private const string ParameterListCloseWithEmptyBody = """
        )
                {
                }
        """;

    /// <summary>Closes an argument list, ends the call statement, and closes the method holding it.</summary>
    private const string ArgumentListCloseAndMethodEnd = """
        );
                }
        """;

    /// <summary>Closes the body of the generated inner method on a line of its own.</summary>
    private const string MethodBodyClose = "        }";

    /// <summary>The accumulating buffer holding the generated source code.</summary>
    private readonly StringBuilder _builder = new();

    /// <summary>Writes the standard usings, namespace and opening class declaration to the buffer.</summary>
    public void Init() =>
        _builder.AppendLine("""
                            using System;
                            using System.Collections.Generic;
                            using System.Linq;
                            using System.Text;
                            using System.Threading.Tasks;
                            using System.Diagnostics;

                            namespace ConsoleApplication1
                            {
                                public class MyTestClass
                                {
                            """);

    /// <summary>Appends a method declaration whose parameters are all on one line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    public void MethodDeclaration(int parameterCount) =>
        _builder.Append(MethodDeclarationOpening)
            .Append(GenerateOneLineParameters(parameterCount))
            .AppendLine(ParameterListCloseWithEmptyBody);

    /// <summary>Appends a method declaration whose parameters are split unevenly across lines.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    /// <returns>The expected diagnostic span as (StartLine, StartColumn, EndLine, EndColumn).</returns>
    public (int StartLine, int StartColumn, int EndLine, int EndColumn) MethodDeclarationJagged(int parameterCount)
    {
        _builder.Append(MethodDeclarationOpening)
            .Append(GenerateJaggedLineParameters(parameterCount))
            .AppendLine(ParameterListCloseWithEmptyBody);

        const int StartLine = 13;
        const int EndLine = StartLine + 3;
        const int StartColumn = 9;
        const int EndColumn = 10;

        return (StartLine, StartColumn, EndLine, EndColumn);
    }

    /// <summary>Appends a method declaration with each parameter on its own line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    public void MethodDeclarationStaggered(int parameterCount) =>
        _builder.AppendLine(MethodDeclarationOpening)
            .Append(GenerateStaggeredLineParameters(parameterCount))
            .AppendLine(ParameterListCloseWithEmptyBody);

    /// <summary>Appends a constructor declaration whose parameters are all on one line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    public void ConstructorDeclaration(int parameterCount) =>
        _builder.Append(ConstructorDeclarationOpening)
            .Append(GenerateOneLineParameters(parameterCount))
            .AppendLine(ParameterListCloseWithEmptyBody);

    /// <summary>Appends a constructor declaration whose parameters are split unevenly across lines.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    /// <returns>The expected diagnostic span as (StartLine, StartColumn, EndLine, EndColumn).</returns>
    public (int StartLine, int StartColumn, int EndLine, int EndColumn) ConstructorDeclarationJagged(int parameterCount)
    {
        _builder.Append(ConstructorDeclarationOpening)
            .Append(GenerateJaggedLineParameters(parameterCount))
            .AppendLine(ParameterListCloseWithEmptyBody);

        const int StartLine = 13;
        const int EndLine = StartLine + 3;
        const int StartColumn = 9;
        const int EndColumn = 10;

        return (StartLine, StartColumn, EndLine, EndColumn);
    }

    /// <summary>Appends a constructor declaration with each parameter on its own line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    public void ConstructorDeclarationStaggered(int parameterCount) =>
        _builder.AppendLine(ConstructorDeclarationOpening)
            .Append(GenerateStaggeredLineParameters(parameterCount))
            .AppendLine(ParameterListCloseWithEmptyBody);

    /// <summary>Appends a delegate declaration whose parameters are all on one line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    public void DelegateDeclaration(int parameterCount) =>
        _builder.Append(DelegateDeclarationOpening).Append(GenerateOneLineParameters(parameterCount)).AppendLine(");");

    /// <summary>Appends a delegate declaration whose parameters are split unevenly across lines.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    /// <returns>The expected diagnostic span as (StartLine, StartColumn, EndLine, EndColumn).</returns>
    public (int StartLine, int StartColumn, int EndLine, int EndColumn) DelegateDeclarationJagged(int parameterCount)
    {
        var input = $"        public delegate void DelegateDefinition({GenerateJaggedLineParameters(parameterCount)});";

        var splitLines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[^1].Length + 1;
        const int StartLine = 12;
        const int EndLine = StartLine + 1;
        const int StartColumn = 9;

        _builder.AppendLine(input);
        return (StartLine, StartColumn, EndLine, endColumn);
    }

    /// <summary>Appends a delegate declaration with each parameter on its own line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    public void DelegateDeclarationStaggered(int parameterCount) =>
        _builder.AppendLine("        public delegate void DelegateDefinition(")
            .Append(GenerateStaggeredLineParameters(parameterCount)).AppendLine(");");

    /// <summary>Appends a delegate plus an anonymous method whose parameters are all on one line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    public void AnonymousMethodExpression(int parameterCount) =>
        _builder.Append(DelegateDeclarationOpening).Append(GenerateOneLineParameters(parameterCount)).Append("""
            );
                    public void MyInnerMethod()
                    {
                        DelegateDefinition action = delegate(
            """).Append(GenerateOneLineParameters(parameterCount)).AppendLine("""
                  )
                              {
                              };
                          }
                  """);

    /// <summary>Appends a delegate plus an anonymous method whose parameters are split unevenly across lines.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    /// <returns>The expected diagnostic span as (StartLine, StartColumn, EndLine, EndColumn).</returns>
    public (int StartLine, int StartColumn, int EndLine, int EndColumn) AnonymousMethodExpressionJagged(int parameterCount)
    {
        var input = $$"""
                                  DelegateDefinition action = delegate({{GenerateJaggedLineParameters(parameterCount)}})
                                  {
                                  };
                      """;

        _builder.Append(DelegateDeclarationOpening).Append(GenerateOneLineParameters(parameterCount)).Append("""
            );
                    public void MyInnerMethod()
                    {

            """).Append(input).AppendLine("""

                                                  }
                                          """);

        var splitLines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[^1].Length;
        const int StartLine = 16;
        const int EndLine = StartLine + 3;
        const int StartColumn = 41;

        return (StartLine, StartColumn, EndLine, endColumn);
    }

    /// <summary>Appends a delegate plus an anonymous method with each parameter on its own line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    public void AnonymousMethodExpressionStaggered(int parameterCount) =>
        _builder.Append(DelegateDeclarationOpening).Append(GenerateOneLineParameters(parameterCount)).AppendLine("""
            );
                    public void MyInnerMethod()
                    {
                        DelegateDefinition action = delegate (
            """).Append(GenerateStaggeredLineParameters(parameterCount, NestedBodyIndentSpaces)).AppendLine("""
            )
                        {
                        };
                    }
            """);

    /// <summary>Appends a parenthesized lambda whose parameters are all on one line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    public void ParenthesizedLambdaExpression(int parameterCount) =>
        _builder.Append("""

                                public void MyInnerMethod()
                                {
                                    var action = (
                        """).Append(GenerateOneLineParameters(parameterCount)).AppendLine("""
            ) =>
                        {
                        };
                    }
            """);

    /// <summary>Appends a parenthesized lambda whose parameters are split unevenly across lines.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    /// <returns>The expected diagnostic span as (StartLine, StartColumn, EndLine, EndColumn).</returns>
    public (int StartLine, int StartColumn, int EndLine, int EndColumn) ParenthesizedLambdaExpressionJagged(int parameterCount)
    {
        var input = $$"""
                                  var action = ({{GenerateJaggedLineParameters(parameterCount)}}) =>
                                  {
                                  };
                      """;

        _builder.Append("""

                                public void MyInnerMethod()
                                {

                        """).Append(input).AppendLine("""

                                                              }
                                                      """);

        var splitLines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[^1].Length;
        const int StartLine = 15;
        const int EndLine = StartLine + 3;
        const int StartColumn = 26;

        return (StartLine, StartColumn, EndLine, endColumn);
    }

    /// <summary>Appends a parenthesized lambda with each parameter on its own line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    public void ParenthesizedLambdaExpressionStaggered(int parameterCount) =>
        _builder.AppendLine("""

                                    public void MyInnerMethod()
                                    {
                                        var action = (
                            """).Append(GenerateStaggeredLineParameters(parameterCount, NestedBodyIndentSpaces)).AppendLine("""
            ) =>
                        {
                        };
                    }
            """);

    /// <summary>Appends an indexer declaration whose parameters are all on one line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    public void IndexerDeclaration(int parameterCount) =>
        _builder.Append("""

                                public int this[
                        """).Append(GenerateOneLineParameters(parameterCount)).AppendLine("] => default;");

    /// <summary>Appends an indexer declaration whose parameters are split unevenly across lines.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    /// <returns>The expected diagnostic span as (StartLine, StartColumn, EndLine, EndColumn).</returns>
    public (int StartLine, int StartColumn, int EndLine, int EndColumn) IndexerDeclarationJagged(int parameterCount)
    {
        var input = $"""

                             public int this[{GenerateJaggedLineParameters(parameterCount)}] => default;
                     """;

        var splitLines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[^1].Length + 1;

        _builder.AppendLine(input);

        const int StartLine = 13;
        const int EndLine = StartLine + 1;
        const int StartColumn = 9;

        return (StartLine, StartColumn, EndLine, endColumn);
    }

    /// <summary>Appends an indexer declaration with each parameter on its own line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    public void IndexerDeclarationStaggered(int parameterCount) =>
        _builder.AppendLine("""

                                    public int this[
                            """).Append(GenerateStaggeredLineParameters(parameterCount)).AppendLine("] => default;");

    /// <summary>Appends a method plus an invocation whose arguments are all on one line.</summary>
    /// <param name="parameterCount">The number of parameters and arguments to emit.</param>
    public void InvocationExpression(int parameterCount)
    {
        MethodDeclaration(parameterCount);
        _builder.Append("""

                                public void MyInnerMethod()
                                {
                                    MyMethod(
                        """).Append(GenerateOneLineArguments(parameterCount)).AppendLine(ArgumentListCloseAndMethodEnd);
    }

    /// <summary>Appends a method plus an invocation whose arguments are split unevenly across lines.</summary>
    /// <param name="parameterCount">The number of parameters and arguments to emit.</param>
    /// <returns>The expected diagnostic span as (StartLine, StartColumn, EndLine, EndColumn).</returns>
    public (int StartLine, int StartColumn, int EndLine, int EndColumn) InvocationExpressionJagged(int parameterCount)
    {
        MethodDeclaration(parameterCount);
        var jaggedParameters = GenerateJaggedLineArguments(parameterCount);
        var input = $$"""

                              public void MyInnerMethod()
                              {
                                  MyMethod({{jaggedParameters}});
                      """;
        _builder.AppendLine(input)
            .AppendLine(MethodBodyClose);

        var splitLines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[^1].Length;
        const int StartLine = 19;
        const int EndLine = StartLine + 1;
        const int StartColumn = 13;

        return (StartLine, StartColumn, EndLine, endColumn);
    }

    /// <summary>Appends a method plus an invocation with each argument on its own line.</summary>
    /// <param name="parameterCount">The number of parameters and arguments to emit.</param>
    public void InvocationExpressionStaggered(int parameterCount)
    {
        MethodDeclaration(parameterCount);
        _builder.AppendLine("""

                                    public void MyInnerMethod()
                                    {
                                        MyMethod(
                            """).Append(GenerateStaggeredLineArguments(parameterCount, NestedBodyIndentSpaces)).AppendLine(ArgumentListCloseAndMethodEnd);
    }

    /// <summary>Appends a nested type plus an object-creation expression whose arguments are all on one line.</summary>
    /// <param name="parameterCount">The number of parameters and arguments to emit.</param>
    public void ObjectCreationExpression(int parameterCount) =>
        _builder.Append(NestedTypeWithConstructorOpening).Append(GenerateOneLineParameters(parameterCount)).Append("""
            )
                        {
                        }
                    }

                    public void MyInnerMethod()
                    {
                        var myInnerTest = new MyInnerTest(
            """).Append(GenerateOneLineArguments(parameterCount)).AppendLine(ArgumentListCloseAndMethodEnd);

    /// <summary>Appends a nested type plus an object-creation expression whose arguments are split unevenly across lines.</summary>
    /// <param name="parameterCount">The number of parameters and arguments to emit.</param>
    /// <returns>The expected diagnostic span as (StartLine, StartColumn, EndLine, EndColumn).</returns>
    public (int StartLine, int StartColumn, int EndLine, int EndColumn) ObjectCreationExpressionJagged(int parameterCount)
    {
        _builder.Append(NestedTypeWithConstructorOpening).Append(GenerateOneLineParameters(parameterCount)).AppendLine("""
            )
                        {
                        }
                    }
            """);
        var jaggedParameters = GenerateJaggedLineArguments(parameterCount);
        var input = $$"""

                              public void MyInnerMethod()
                              {
                                  var myInnerTest = new MyInnerTest({{jaggedParameters}});
                      """;
        _builder.AppendLine(input)
            .AppendLine(MethodBodyClose);

        var splitLines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[^1].Length;
        const int StartLine = 22;
        const int EndLine = StartLine + 1;
        const int StartColumn = 31;

        return (StartLine, StartColumn, EndLine, endColumn);
    }

    /// <summary>Appends a nested type plus an object-creation expression with each argument on its own line.</summary>
    /// <param name="parameterCount">The number of parameters and arguments to emit.</param>
    public void ObjectCreationExpressionStaggered(int parameterCount) =>
        _builder.Append(NestedTypeWithConstructorOpening).Append(GenerateOneLineParameters(parameterCount)).AppendLine("""
            )
                        {
                        }
                    }

                    public void MyInnerMethod()
                    {
                        var myInnerTest = new MyInnerTest(
            """).Append(GenerateStaggeredLineArguments(parameterCount, NestedBodyIndentSpaces)).AppendLine(ArgumentListCloseAndMethodEnd);

    /// <summary>Appends an attribute type plus its usage whose arguments are all on one line.</summary>
    /// <param name="parameterCount">The number of parameters and arguments to emit.</param>
    public void Attribute(int parameterCount) =>
        _builder.Append(AttributeTypeWithConstructorOpening).Append(GenerateOneLineParameters(parameterCount)).Append("""
            )
                        {
                        }
                    }

                    [MyInnerTest(
            """).Append(GenerateOneLineArguments(parameterCount)).AppendLine("""
                                                                             )]
                                                                                     public void MyInnerMethod()
                                                                                     {
                                                                                     }
                                                                             """);

    /// <summary>Appends an attribute type plus its usage whose arguments are split unevenly across lines.</summary>
    /// <param name="parameterCount">The number of parameters and arguments to emit.</param>
    /// <returns>The expected diagnostic span as (StartLine, StartColumn, EndLine, EndColumn).</returns>
    public (int StartLine, int StartColumn, int EndLine, int EndColumn) AttributeJagged(int parameterCount)
    {
        var input = $"        [MyInnerTest({GenerateJaggedLineArguments(parameterCount)})]";

        _builder.Append(AttributeTypeWithConstructorOpening).Append(GenerateOneLineParameters(parameterCount)).Append("""
            )
                        {
                        }
                    }


            """).Append(input).AppendLine("""

                                                  public void MyInnerMethod()
                                                  {
                                                  }
                                          """);

        var splitLines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[^1].Length;
        const int StartLine = 20;
        const int EndLine = StartLine + 1;
        const int StartColumn = 10;

        return (StartLine, StartColumn, EndLine, endColumn);
    }

    /// <summary>Appends an attribute type plus its usage with each argument on its own line.</summary>
    /// <param name="parameterCount">The number of parameters and arguments to emit.</param>
    public void AttributeStaggered(int parameterCount) =>
        _builder.Append(AttributeTypeWithConstructorOpening).Append(GenerateOneLineParameters(parameterCount)).AppendLine("""
            )
                        {
                        }
                    }

                    [MyInnerTest(
            """).Append(GenerateStaggeredLineArguments(parameterCount)).AppendLine("""
            )]
                    public void MyInnerMethod()
                    {
                    }
            """);

    /// <summary>Appends element-access expressions whose arguments are all on one line.</summary>
    /// <param name="parameterCount">The number of arguments to emit.</param>
    public void ElementAccessExpression(int parameterCount) =>
        _builder.Append("""

                                public void MyInnerMethod()
                                {
                                    var myArray = new int[
                        """).Append(GenerateOneLineArguments(parameterCount)).Append("""
            ];
                        myArray[
            """).Append(GenerateOneLineArguments(parameterCount)).AppendLine("""
                                                                             ] = 1;
                                                                                     }
                                                                             """);

    /// <summary>Appends element-access expressions whose arguments are split unevenly across lines.</summary>
    /// <param name="parameterCount">The number of arguments to emit.</param>
    /// <returns>The expected diagnostic span as (StartLine, StartColumn, EndLine, EndColumn).</returns>
    public (int StartLine, int StartColumn, int EndLine, int EndColumn) ElementAccessExpressionJagged(int parameterCount)
    {
        var jaggedParameters = GenerateJaggedLineArguments(parameterCount);
        var nonJaggedParameters = GenerateOneLineArguments(parameterCount);
        var input = $$"""

                              public void MyInnerMethod()
                              {
                                  var myArray = new int[{{nonJaggedParameters}}];
                                  myArray[{{jaggedParameters}}] = 1;
                      """;
        _builder.AppendLine(input)
            .AppendLine(MethodBodyClose);

        var splitLines = input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        const int TrailingAssignmentColumns = 4;
        var endColumn = splitLines[^1].Length - TrailingAssignmentColumns;
        const int StartLine = 16;
        const int EndLine = StartLine + 1;
        const int StartColumn = 13;

        return (StartLine, StartColumn, EndLine, endColumn);
    }

    /// <summary>Appends element-access expressions with each argument on its own line.</summary>
    /// <param name="parameterCount">The number of arguments to emit.</param>
    public void ElementAccessExpressionStaggered(int parameterCount) =>
        _builder.Append("""

                                public void MyInnerMethod()
                                {
                                    var myArray = new int[
                        """).Append(GenerateOneLineArguments(parameterCount)).AppendLine("""
            ];
                        myArray[
            """).Append(GenerateStaggeredLineArguments(parameterCount, NestedBodyIndentSpaces)).AppendLine("""
            ] = 1;
                    }
            """);

    /// <summary>Closes the class and namespace and returns the complete generated source code.</summary>
    /// <returns>The full generated C# source as a string.</returns>
    public string Generate()
    {
        _builder.AppendLine("""
                                }
                            }
                            """);

        return _builder.ToString();
    }

    /// <summary>Builds a comma-separated parameter list rendered on a single line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    /// <returns>The single-line parameter list text.</returns>
    private static string GenerateOneLineParameters(int parameterCount) => string.Join(", ", Enumerable.Range(0, parameterCount).Select(static i =>
        $"int a{i}"));

    /// <summary>Builds a parameter list split unevenly across two lines.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    /// <returns>The jagged-line parameter list text.</returns>
    private static string GenerateJaggedLineParameters(int parameterCount)
    {
        var halfWayPoint = parameterCount / JaggedLineCount;
        var remainingCount = parameterCount - halfWayPoint;

        return $"""
                {string.Join(", ", Enumerable.Range(0, halfWayPoint).Select(static i => $"int a{i}"))},
                            {string.Join(", ", Enumerable.Range(halfWayPoint, remainingCount).Select(static i => $"int a{i}"))}
                """;
    }

    /// <summary>Builds a parameter list with each parameter on its own indented line.</summary>
    /// <param name="parameterCount">The number of parameters to emit.</param>
    /// <param name="whitespaceCount">The number of leading spaces to indent each parameter line.</param>
    /// <returns>The staggered-line parameter list text.</returns>
    private static string GenerateStaggeredLineParameters(int parameterCount, int whitespaceCount = 12)
    {
        var stringBuilder = new StringBuilder();

        var whitespace = new string(' ', whitespaceCount);

        for (var i = 0; i < parameterCount - 1; ++i)
        {
            stringBuilder.Append(whitespace).Append("int ").Append('a').Append(i).AppendLine(",");
        }

        stringBuilder.Append(whitespace).Append("int ").Append('a').Append(parameterCount - 1);

        return stringBuilder.ToString();
    }

    /// <summary>Builds a comma-separated argument list rendered on a single line.</summary>
    /// <param name="parameterCount">The number of arguments to emit.</param>
    /// <returns>The single-line argument list text.</returns>
    private static string GenerateOneLineArguments(int parameterCount) => string.Join(", ", Enumerable.Range(0, parameterCount).Select(static i => $"{i}"));

    /// <summary>Builds an argument list split unevenly across two lines.</summary>
    /// <param name="parameterCount">The number of arguments to emit.</param>
    /// <returns>The jagged-line argument list text.</returns>
    private static string GenerateJaggedLineArguments(int parameterCount)
    {
        var halfWayPoint = parameterCount / JaggedLineCount;
        var remainingCount = parameterCount - halfWayPoint;

        return $"""
                {string.Join(", ", Enumerable.Range(0, halfWayPoint).Select(static i => $"{i}"))},
                            {string.Join(", ", Enumerable.Range(halfWayPoint, remainingCount).Select(static i => $"{i}"))}
                """;
    }

    /// <summary>Builds an argument list with each argument on its own indented line.</summary>
    /// <param name="parameterCount">The number of arguments to emit.</param>
    /// <param name="whitespaceCount">The number of leading spaces to indent each argument line.</param>
    /// <returns>The staggered-line argument list text.</returns>
    private static string GenerateStaggeredLineArguments(int parameterCount, int whitespaceCount = 12)
    {
        var stringBuilder = new StringBuilder();
        var whitespace = new string(' ', whitespaceCount);

        for (var i = 0; i < parameterCount - 1; ++i)
        {
            stringBuilder.Append(whitespace).Append(i).AppendLine(",");
        }

        stringBuilder.Append(whitespace).Append(parameterCount - 1);

        return stringBuilder.ToString();
    }
}
