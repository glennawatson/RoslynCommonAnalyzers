// Copyright (c) 2023 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace Blazor.Common.Analyzers.Tests;

internal class ClassGeneratorBuilder
{
    private StringBuilder _builder = new();

    public void Init()
    {
        _builder.AppendLine(@"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ConsoleApplication1
{
    public class MyTestClass
    {");
    }

    public void MethodDeclaration(int parameterCount)
    {
        _builder.Append(@"
        public void MyMethod(").Append(GenerateOneLineParameters(parameterCount)).AppendLine(@")
        {
        }");
    }

    public (int StartLine, int StartColumn, int EndLine, int EndColumn) MethodDeclarationJaggered(int parameterCount)
    {
        _builder.Append(@"
        public void MyMethod(").Append(GenerateJaggeredLineParameters(parameterCount)).AppendLine(@")
        {
        }");

        return (13, 9, 16, 10);
    }

    public void MethodDeclarationStaggered(int parameterCount)
    {
        _builder.AppendLine(@"
        public void MyMethod(").Append(GenerateStaggeredLineParameters(parameterCount)).AppendLine(@")
        {
        }");
    }

    public void ConstructorDeclaration(int parameterCount)
    {
        _builder.Append(@"
        public MyTestClass(").Append(GenerateOneLineParameters(parameterCount)).AppendLine(@")
        {
        }");
    }

    public (int StartLine, int StartColumn, int EndLine, int EndColumn) ConstructorDeclarationJaggered(int parameterCount)
    {
        _builder.Append(@"
        public MyTestClass(").Append(GenerateJaggeredLineParameters(parameterCount)).AppendLine(@")
        {
        }");
        return (13, 9, 16, 10);
    }

    public void ConstructorDeclarationStaggered(int parameterCount)
    {
        _builder.AppendLine(@"
        public MyTestClass(").Append(GenerateStaggeredLineParameters(parameterCount)).AppendLine(@")
        {
        }");
    }

    public void DelegateDeclaration(int parameterCount)
    {
        _builder.Append(@"
        public delegate void DelegateDefinition(").Append(GenerateOneLineParameters(parameterCount)).AppendLine(");");
    }

    public (int StartLine, int StartColumn, int EndLine, int EndColumn) DelegateDeclarationJaggered(int parameterCount)
    {
        var input = $"        public delegate void DelegateDefinition({GenerateJaggeredLineParameters(parameterCount)});";

        var splitLines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[splitLines.Length - 1].Length + 1;
        var startLine = 12;
        var endLine = startLine + 1;
        var startColumn = 9;

        _builder.AppendLine(input);
        return (startLine, startColumn, endLine, endColumn);
    }

    public void DelegateDeclarationStaggered(int parameterCount)
    {
        _builder.AppendLine("        public delegate void DelegateDefinition(").Append(GenerateStaggeredLineParameters(parameterCount)).AppendLine(");");
    }

    public void AnonymousMethodExpression(int parameterCount)
    {
        _builder.Append(@"
        public delegate void DelegateDefinition(").Append(GenerateOneLineParameters(parameterCount)).Append(@");
        public void MyInnerMethod()
        {
            DelegateDefinition action = delegate(").Append(GenerateOneLineParameters(parameterCount)).AppendLine(@")
            {
            };
        }");
    }

    public (int StartLine, int StartColumn, int EndLine, int EndColumn) AnonymousMethodExpressionJaggered(int parameterCount)
    {
        var input = @$"            DelegateDefinition action = delegate({GenerateJaggeredLineParameters(parameterCount)})
            {{
            }};";

        _builder.Append(@"
        public delegate void DelegateDefinition(").Append(GenerateOneLineParameters(parameterCount)).Append(@");
        public void MyInnerMethod()
        {
").Append(input).AppendLine(@"
        }");

        var splitLines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[splitLines.Length - 1].Length;
        var startLine = 16;
        var endLine = startLine + 3;
        var startColumn = 41;

        return (startLine, startColumn, endLine, endColumn);
    }

    public void AnonymousMethodExpressionStaggered(int parameterCount)
    {
        _builder.Append(@"
        public delegate void DelegateDefinition(").Append(GenerateOneLineParameters(parameterCount)).AppendLine(@");
        public void MyInnerMethod()
        {
            DelegateDefinition action = delegate (").Append(GenerateStaggeredLineParameters(parameterCount, 16)).AppendLine(@")
            {
            };
        }");
    }

    public void ParenthesizedLambdaExpression(int parameterCount)
    {
        _builder.Append(@"
        public void MyInnerMethod()
        {
            var action = (").Append(GenerateOneLineParameters(parameterCount)).AppendLine(@") =>
            {
            };
        }");
    }

    public (int StartLine, int StartColumn, int EndLine, int EndColumn) ParenthesizedLambdaExpressionJaggered(int parameterCount)
    {
        var input = @$"            var action = ({GenerateJaggeredLineParameters(parameterCount)}) =>
            {{
            }};";

        _builder.Append(@"
        public void MyInnerMethod()
        {
").Append(input).AppendLine(@"
        }");

        var splitLines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[splitLines.Length - 1].Length;
        var startLine = 15;
        var endLine = startLine + 3;
        var startColumn = 26;

        return (startLine, startColumn, endLine, endColumn);
    }

    public void ParenthesizedLambdaExpressionStaggered(int parameterCount)
    {
        _builder.AppendLine(@"
        public void MyInnerMethod()
        {
            var action = (").Append(GenerateStaggeredLineParameters(parameterCount, 16)).AppendLine(@") =>
            {
            };
        }");
    }

    public void IndexerDeclaration(int parameterCount)
    {
        _builder.Append(@"
        public int this[").Append(GenerateOneLineParameters(parameterCount)).AppendLine("] => default;");
    }

    public (int StartLine, int StartColumn, int EndLine, int EndColumn) IndexerDeclarationJaggered(int parameterCount)
    {
        var input = $@"
        public int this[{GenerateJaggeredLineParameters(parameterCount)}] => default;";

        var splitLines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[splitLines.Length - 1].Length + 1;

        _builder.AppendLine(input);

        return (13, 9, 14, endColumn);
    }

    public void IndexerDeclarationStaggered(int parameterCount)
    {
        _builder.AppendLine(@"
        public int this[").Append(GenerateStaggeredLineParameters(parameterCount)).AppendLine("] => default;");
    }

    public void InvocationExpression(int parameterCount)
    {
        MethodDeclaration(parameterCount);
        _builder.Append(@"
        public void MyInnerMethod()
        {
            MyMethod(").Append(GenerateOneLineArguments(parameterCount)).AppendLine(@");
        }");
    }

    public (int StartLine, int StartColumn, int EndLine, int EndColumn) InvocationExpressionJaggered(int parameterCount)
    {
        MethodDeclaration(parameterCount);
        var jaggeredParameters = GenerateJaggeredLineArguments(parameterCount);
        var input = $@"
        public void MyInnerMethod()
        {{
            MyMethod({jaggeredParameters});";
        _builder.AppendLine(input)
            .AppendLine("        }");

        var splitLines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[splitLines.Length - 1].Length;
        var startLine = 19;
        var endLine = startLine + 1;
        var startColumn = 13;

        return (startLine, startColumn, endLine, endColumn);
    }

    public void InvocationExpressionStaggered(int parameterCount)
    {
        MethodDeclaration(parameterCount);
        _builder.AppendLine(@"
        public void MyInnerMethod()
        {
            MyMethod(").Append(GenerateStaggeredLineArguments(parameterCount, 16)).AppendLine(@");
        }");
    }

    public void ObjectCreationExpression(int parameterCount)
    {
        _builder.Append(@"
        public class MyInnerTest
        {
            public MyInnerTest(").Append(GenerateOneLineParameters(parameterCount)).Append(@")
            {
            }
        }
    
        public void MyInnerMethod()
        {
            var myInnerTest = new MyInnerTest(").Append(GenerateOneLineArguments(parameterCount)).AppendLine(@");
        }");
    }

    public (int StartLine, int StartColumn, int EndLine, int EndColumn) ObjectCreationExpressionJaggered(int parameterCount)
    {
        _builder.Append(@"
        public class MyInnerTest
        {
            public MyInnerTest(").Append(GenerateOneLineParameters(parameterCount)).AppendLine(@")
            {
            }
        }");
        var jaggeredParameters = GenerateJaggeredLineArguments(parameterCount);
        var input = $@"
        public void MyInnerMethod()
        {{
            var myInnerTest = new MyInnerTest({jaggeredParameters});";
        _builder.AppendLine(input)
            .AppendLine("        }");

        var splitLines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[splitLines.Length - 1].Length;
        var startLine = 22;
        var endLine = startLine + 1;
        var startColumn = 31;

        return (startLine, startColumn, endLine, endColumn);
    }

    public void ObjectCreationExpressionStaggered(int parameterCount)
    {
        _builder.Append(@"
        public class MyInnerTest
        {
            public MyInnerTest(").Append(GenerateOneLineParameters(parameterCount)).AppendLine(@")
            {
            }
        }

        public void MyInnerMethod()
        {
            var myInnerTest = new MyInnerTest(").Append(GenerateStaggeredLineArguments(parameterCount, 16)).AppendLine(@");
        }");
    }

    public void Attribute(int parameterCount)
    {
        _builder.Append(@"
        public class MyInnerTestAttribute : System.Attribute
        {
            public MyInnerTestAttribute(").Append(GenerateOneLineParameters(parameterCount)).Append(@")
            {
            }
        }
    
        [MyInnerTest(").Append(GenerateOneLineArguments(parameterCount)).AppendLine(@")]
        public void MyInnerMethod()
        {
        }");
    }

    public (int StartLine, int StartColumn, int EndLine, int EndColumn) AttributeJaggered(int parameterCount)
    {
        var input = $"        [MyInnerTest({GenerateJaggeredLineArguments(parameterCount)})]";

        _builder.Append(@"
        public class MyInnerTestAttribute : System.Attribute
        {
            public MyInnerTestAttribute(").Append(GenerateOneLineParameters(parameterCount)).Append(@")
            {
            }
        }
    
").Append(input).AppendLine(@"
        public void MyInnerMethod()
        {
        }");

        var splitLines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[splitLines.Length - 1].Length;
        var startLine = 20;
        var endLine = startLine + 1;
        var startColumn = 10;

        return (startLine, startColumn, endLine, endColumn);
    }

    public void AttributeStaggered(int parameterCount)
    {
        _builder.Append(@"
        public class MyInnerTestAttribute : System.Attribute
        {
            public MyInnerTestAttribute(").Append(GenerateOneLineParameters(parameterCount)).AppendLine(@")
            {
            }
        }
    
        [MyInnerTest(").Append(GenerateStaggeredLineArguments(parameterCount)).AppendLine(@")]
        public void MyInnerMethod()
        {
        }");
    }

    public void ElementAccessExpression(int parameterCount)
    {
        _builder.Append(@"
        public void MyInnerMethod()
        {
            var myArray = new int[").Append(GenerateOneLineArguments(parameterCount)).Append(@"];
            myArray[").Append(GenerateOneLineArguments(parameterCount)).AppendLine(@"] = 1;
        }");
    }

    public (int StartLine, int StartColumn, int EndLine, int EndColumn) ElementAccessExpressionJaggered(int parameterCount)
    {
        var jaggeredParameters = GenerateJaggeredLineArguments(parameterCount);
        var nonJaggeredParameters = GenerateOneLineArguments(parameterCount);
        var input = $@"
        public void MyInnerMethod()
        {{
            var myArray = new int[{nonJaggeredParameters}];
            myArray[{jaggeredParameters}] = 1;";
        _builder.AppendLine(input)
            .AppendLine("        }");

        var splitLines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var endColumn = splitLines[splitLines.Length - 1].Length - 4;
        var startLine = 16;
        var endLine = startLine + 1;
        var startColumn = 13;

        return (startLine, startColumn, endLine, endColumn);
    }

    public void ElementAccessExpressionStaggered(int parameterCount)
    {
        _builder.Append(@"
        public void MyInnerMethod()
        {
            var myArray = new int[").Append(GenerateOneLineArguments(parameterCount)).AppendLine(@"];
            myArray[").Append(GenerateStaggeredLineArguments(parameterCount, 16)).AppendLine(@"] = 1;
        }");
    }

    public string Generate()
    {
        _builder.AppendLine(@"    }
}");

        return _builder.ToString();
    }

    private static string GenerateOneLineParameters(int parameterCount)
    {
        return string.Join(", ", Enumerable.Range(0, parameterCount).Select(i => $"int a{i}"));
    }

    private static string GenerateJaggeredLineParameters(int parameterCount)
    {
        var halfWayPoint = parameterCount / 2;
        var remainingCount = parameterCount - halfWayPoint;

        return $@"{string.Join(", ", Enumerable.Range(0, halfWayPoint).Select(i => $"int a{i}"))},
            {string.Join(", ", Enumerable.Range(halfWayPoint, remainingCount).Select(i => $"int a{i}"))}";
    }

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

    private static string GenerateOneLineArguments(int parameterCount)
    {
        return string.Join(", ", Enumerable.Range(0, parameterCount).Select(i => $"{i}"));
    }

    private static string GenerateJaggeredLineArguments(int parameterCount)
    {
        var halfWayPoint = parameterCount / 2;
        var remainingCount = parameterCount - halfWayPoint;

        return $@"{string.Join(", ", Enumerable.Range(0, halfWayPoint).Select(i => $"{i}"))},
            {string.Join(", ", Enumerable.Range(halfWayPoint, remainingCount).Select(i => $"{i}"))}";
    }

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
