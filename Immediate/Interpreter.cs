using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using Microsoft.Samples.Debugging.CorDebug;

public class InterpreterResult
{
    public List<(string name, object value)> addedToStore { get; set; }
    public object Result { get; set; }
}

public class Interpreter
{
    private readonly ISymbolReader _reader;
    private readonly CorFrame _frame;

    private Dictionary<string, int> LocalVariables => new Lazy<Dictionary<string, int>>(() => _reader
        .GetMethod(new SymbolToken(_frame.FunctionToken)).RootScope
        .GetChildren().First().GetLocals()
        .Select((x, i) => new {x, i})
        .ToDictionary(x => x.x.Name, x => x.i)).Value;

    public Interpreter(ISymbolReader reader, CorFrame frame)
    {
        _reader = reader;
        _frame = frame;
    }

    private static string[] ParseVariable(string expression) => expression.Split('+').Select(x => x.Trim()).ToArray();

    public object InterpretExpression(string expression)
    {
        if (int.TryParse(expression, out var value)) return value;
        var variables = ParseVariable(expression);

        if (variables.Length == 2)
        {
            var fstValue = (int) _frame.GetLocalVariable(LocalVariables[variables[0]]).CastToGenericValue().GetValue();
            var sndValue = (int) _frame.GetLocalVariable(LocalVariables[variables[1]]).CastToGenericValue().GetValue();
            return fstValue + sndValue;
        }

        return null;
    }

    public InterpreterResult InterpretStatement(string statement)
    {
        statement = statement.Substring("var".Length);
        var splitted = statement.Split('=');
        var varName = splitted[0].Trim();
        var value = splitted[1].Trim();
        var result = InterpretExpression(value);
        return new InterpreterResult
        {
            Result = result,
            addedToStore = new List<(string name, object value)>() {(varName, result)}
        };
    }


    public InterpreterResult Interpret(string expression)
    {
        return expression.Contains("var")
            ? InterpretStatement(expression)
            : new InterpreterResult() {Result = InterpretExpression(expression)};
    }
}