using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Slik.Utils;

public class FunctionParser
{
    private static readonly Regex ParseFunctionStringRegex = new(@"; (?<class>\w+)::(?<method>\w+)\((?<params>[^)]*)\)");
    private static readonly Regex ConvertToFunctionRegex = new(@"^\s*(?<returnType>virtual\s+[\w\s]+\*?)\s+(?<name>\w+)\s*\((?<args>[^)]*)\)");
    private static readonly Regex IsValidFunctionRegex = new(@"^\s*\.rodata:[0-9A-F]+\s+((dq\s+offset\s+)?[_A-Z0-9]+\d+\w*\s*)?;\s*\w+::\w+\([^)]*\)?$");
    private static readonly Regex ParseDestructorStringRegex = new(@"; (?<class>\w+)::~(?<classC>\w+)");

    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private static string? _mergePath = "";
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private static string? _defaultReturnType = "void";
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private static bool _includeIndexes = false;
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private static bool _thirtyTwoBit = false;

    public static int[] InOutMap = {}; // Used for textbox in MainWindow
    
    private static bool _alreadyFoundDestructor;


    public enum ParseReturnType
    {
        Vftable,
        Indexes,
        Both
    }

    #region Function Parsing Methods

    private static Function ParseFunctionString(string input)
    {
        // Use a regular expression to extract the class name, method name, and parameters
        Match match = ParseFunctionStringRegex.Match(input);

        // Extract the class name, method name, and parameters from the regex match
        string className = match.Groups["class"].Value;
        string methodName = match.Groups["method"].Value;
        string methodParams = match.Groups["params"].Value.Replace(",", ", ").Replace(" &", "&").Replace(" *", "*");

        return new(className, methodName, methodParams);
    }

    private static Function? ConvertToFunction(string input)
    {
        Match match = ConvertToFunctionRegex.Match(input);

        if (!match.Success) return null;

        Function obj = new("class",
            match.Groups["name"].Value,
            match.Groups["args"].Value,
            match.Groups["returnType"].Value);
        return obj;
    }

    private static bool IsValidFunc(string input)
    {
        return IsValidFunctionRegex.IsMatch(input);
    }


    private static Function? ParseDestructorString(string input)
    {
        if (_alreadyFoundDestructor)
            return null;

        // Player::~Player()
        Match match = ParseDestructorStringRegex.Match(input);

        string className = match.Groups["class"].Value;

        _alreadyFoundDestructor = true;

        return new(className, "~" + className, "");
    }


    private static Function? ParseString(string input, int index)
    {
        if (input.Contains('~') && input.Contains("dq offset")) // Destructor
            return ParseDestructorString(input);

        if (input.Contains("___cxa_pure_virtual")) // Pure virtual function
            return new("Class", "Function" + index, "");


        return !IsValidFunc(input) ? // Not a valid function
            null : ParseFunctionString(input);
    }


    private static string GuessReturnType(string name)
    {
        // Check if the function name starts with "is" or "has"
        if (name.StartsWith("is") || name.StartsWith("has") || name.StartsWith("can"))
        {
            // If the function name starts with "is" or "has", it is likely to return a boolean value
            return "virtual bool";
        }

        // If the function name does not match any of the above patterns, it is likely to be a normal void
        return $"virtual {_defaultReturnType}";
    }
    #endregion

    public static string[]? ParseInput(string input, ParseReturnType returnType = ParseReturnType.Vftable)
    {
        // Read the input file

        string[] lines;

        lines = input.Replace("\r","").Split('\n');
        List<string> functionNames = new();
        List<string> functionIndexes = new();
        List<Function?> functionList1 = new();



        int index = -1; // -1 because of the typeinfo 

        // Read all lines
        InOutMap = new int[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            InOutMap[i] = functionList1.Count;
            string line = lines[i];
            // Parse the line
            Function? function = ParseString(line, index);

            // Check if the line was a valid function
            if (function == null)
            {
                Debug.WriteLine($"Line {i} was not a valid function");
                continue;
            }

            // Guess the return type
            string functionReturnType = GuessReturnType(function.Name);

                
            functionList1.Add(new("class", function.Name, function.Args, functionReturnType));

            index++;
        }


        /*if (!Merge(functionList1))
        {
            // Merge was not successful
            Debug.WriteLine("Merge was not successful");
        }
        else
        {
            // Merge was successful
            Debug.WriteLine("Merge was successful");
        }*/

        int newIndex = 0;

        string lastFunctionName = "";
        Debug.WriteLine("Converting to strings");
        foreach (Function? func in functionList1)
        {
            functionNames.Add($"{func?.ReturnType} {func?.Name}({func?.Args});{(_includeIndexes ? $" // {newIndex} (0x{newIndex * (_thirtyTwoBit ? 4 : 8):X})" : string.Empty)}");

            if (lastFunctionName != func?.Name && !func!.Name.Contains('~'))
                functionIndexes.Add($"int {func.Name} = {newIndex};{(_includeIndexes ? $" // 0x{newIndex * (_thirtyTwoBit ? 4 : 8):X}" : string.Empty)}");


            if (!func.Name.Contains('~'))
                newIndex++;

            lastFunctionName = func.Name;
        }

        
        Debug.WriteLine("Converted");

        return returnType switch
        {
            ParseReturnType.Vftable => functionNames.ToArray(),
            ParseReturnType.Indexes => functionIndexes.ToArray(),
            ParseReturnType.Both => throw new NotImplementedException(),
            _ => null
        };
    }
    
    // ReSharper disable once UnusedMember.Local
    private static bool Merge(List<Function?> functionList1)
    {
        if (_mergePath == null) return false;

        // interpret the vtable from the input file
        if (!File.Exists(_mergePath))
        {
            Debug.WriteLine($"Merge file {_mergePath} does not exist");
            return false;
        }

        Debug.WriteLine("Attempting to merge with " + _mergePath);
        var oldVtableStr = File.ReadAllLines(_mergePath);

        var oldVTableFunctions = oldVtableStr.Select(ConvertToFunction).ToList();

        Debug.WriteLine("Finished parsing old vtable, attempting to merge");
        foreach (Function? newFunc in functionList1)
        {
            if (newFunc == null)
            {
                Debug.WriteLine("Skipping null function in new vtable");
                continue;
            }

            foreach (Function? oldFunc in oldVTableFunctions)
            {
                if (oldFunc == null || newFunc.Changed || oldFunc.Changed)
                {
                    Debug.WriteLine("Skipping a function in the old vtable because:");
                    if (oldFunc == null)
                        Debug.WriteLine("It is null");
                    if (oldFunc?.Changed ?? false)
                        Debug.WriteLine("The old vtable function was already changed");
                    if (newFunc.Changed)
                    {
                        Debug.WriteLine("The new vtable function was already changed");
                        break;
                    }

                    continue;
                }

                if (newFunc.Name != oldFunc.Name) continue;

                if (newFunc.Args != oldFunc.Args)
                {
                    Debug.WriteLine($"Changing args of {newFunc.Name} from {newFunc.Args} to {oldFunc.Args}");
                    newFunc.Args = oldFunc.Args;
                }

                if (newFunc.ReturnType != oldFunc.ReturnType)
                {
                    Debug.WriteLine(
                        $"Changing return type of {newFunc.Name} from {newFunc.ReturnType} to {oldFunc.ReturnType}");
                    newFunc.ReturnType = oldFunc.ReturnType;
                }


                newFunc.Changed = true;
                oldFunc.Changed = true;
            }
        }

        return true;
    }
}