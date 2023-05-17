using System.Collections.Generic;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Slik;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }


    private static readonly Regex ParseFunctionStringRegex = new(@"; (?<class>\w+)::(?<method>\w+)\((?<params>[^)]*)\)");
    private static readonly Regex ConvertToFunctionRegex = new(@"^\s*(?<returnType>virtual\s+[\w\s]+\*?)\s+(?<name>\w+)\s*\((?<args>[^)]*)\)");
    private static readonly Regex IsValidFunctionRegex = new(@"^\s*\.rodata:[0-9A-F]+\s+((dq\s+offset\s+)?[_A-Z0-9]+\d+\w*\s*)?;\s*\w+::\w+\([^)]*\)?$");
    private static readonly Regex ParseDestructorStringRegex = new(@"; (?<class>\w+)::~(?<classC>\w+)");

    private static Function? ParseFunctionString(string input)
    {
        // Use a regular expression to extract the class name, method name, and parameters
        Match match = ParseFunctionStringRegex.Match(input);

        // Extract the class name, method name, and parameters from the regex match
        string className = match.Groups["class"].Value;
        string methodName = match.Groups["method"].Value;
        string methodParams = match.Groups["params"].Value;

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

    private static bool _alreadyFoundDestructor;

    private Function? ParseDestructorString(string input)
    {
        if (_alreadyFoundDestructor)
            return null;

        // Player::~Player()
        Match match = ParseDestructorStringRegex.Match(input);

        string className = match.Groups["class"].Value;

        _alreadyFoundDestructor = true;

        return new(className, "~" + className, "");
    }


    private Function? ParseString(string input, int index)
    {
        if (input.Contains('~') && input.Contains("dq offset")) // Destructor
            return ParseDestructorString(input);

        if (input.Contains("___cxa_pure_virtual")) // Pure virtual function
            return new("Class", "Function" + index, "");


        return !IsValidFunc(input) ? // Not a valid function
            null : ParseFunctionString(input);
    }


    private string GuessReturnType(string name)
    {
        // Check if the function name starts with "is" or "has"
        if (name.StartsWith("is") || name.StartsWith("has") || name.StartsWith("can"))
        {
            // If the function name starts with "is" or "has", it is likely to return a boolean value
            return "virtual Bool";
        }

        // If the function name does not match any of the above patterns, it is likely to be a normal void
        return $"virtual {ReturnGuess}";
    }

    private static string? MergePath;
    private static string? ReturnGuess = "void";
    private static bool IncludeIndexes = true;
    private static bool ThirtyTwoBit;

    public void Convert()
    {
        // Read the input file

        string[] lines;

        lines = LeftTextBox.Text.Replace("\r","").Split('\n');
        List<string> functionNames = new();
        List<string> functionIndexes = new();
        List<Function?> functionList1 = new();



        int index = -1; // -1 because of the typeinfo 

        // Read all lines
        inOutMap = new int[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            inOutMap[i] = functionList1.Count;
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
            string returnType = GuessReturnType(function.Name);

                
            functionList1.Add(new("class", function.Name, function.Args, returnType));

            index++;
        }


        if (MergePath != null)
        {
            // interpret the vtable from the input file

            if (!File.Exists(MergePath))
            {
                Debug.WriteLine($"Merge file {MergePath} does not exist");
                return;
            }

            Debug.WriteLine("Attempting to merge with " + MergePath);
            string[] oldVtableStr = File.ReadAllLines(MergePath);

            List<Function?> oldVTableFunctions = new();

            foreach (string line in oldVtableStr)
            {
                oldVTableFunctions.Add(ConvertToFunction(line));
            }
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
                    if (oldFunc == null || newFunc.wasChanged || oldFunc.wasChanged)
                    {
                        Debug.WriteLine("Skipping a function in the old vtable because:");
                        if (oldFunc == null)
                            Debug.WriteLine("It is null");
                        if (oldFunc?.wasChanged ?? false)
                            Debug.WriteLine("The old vtable function was already changed");
                        if (newFunc.wasChanged)
                        {
                            Debug.WriteLine("The new vtable function was already changed");
                            break;
                        }

                        continue;
                    }

                    if (newFunc.Name != oldFunc.Name) continue;

                    if (newFunc.Args != oldFunc.Args)
                    {
                        Console.WriteLine($"Changing args of {newFunc.Name} from {newFunc.Args} to {oldFunc.Args}");
                        newFunc.Args = oldFunc.Args;
                    }

                    if (newFunc.ReturnType != oldFunc.ReturnType)
                    {
                        Console.WriteLine($"Changing return type of {newFunc.Name} from {newFunc.ReturnType} to {oldFunc.ReturnType}");
                        newFunc.ReturnType = oldFunc.ReturnType;
                    }


                    newFunc.wasChanged = true;
                    oldFunc.wasChanged = true;
                }
            }
        }

        int newIndex = 0;

        string lastFunctionName = "";
        Debug.WriteLine("Converting to strings");
        foreach (Function? func in functionList1)
        {
            functionNames.Add($"{func?.ReturnType} {func?.Name}({func?.Args});{(IncludeIndexes ? $" // {newIndex} (0x{newIndex * (ThirtyTwoBit ? 4 : 8):X})" : string.Empty)}");

            if (lastFunctionName != func?.Name && !func!.Name.Contains('~'))
                functionIndexes.Add($"void {func.Name} = {newIndex};{(IncludeIndexes ? $" // 0x{newIndex * (ThirtyTwoBit ? 4 : 8):X}" : string.Empty)}");


            if (!func.Name.Contains('~'))
                newIndex++;

            lastFunctionName = func.Name;
        }

        if (RightTextBox == null)
            return;
            
        RightTextBox.Text = (string)VtableOrIndexesButton.Content == "vtable" ? string.Join('\n', functionNames) : string.Join('\n', functionIndexes);

        Debug.WriteLine("Converted");
    }

    private static int[] inOutMap = new int[0];

    private void VtableOrIndexesButton_OnClick(object sender, RoutedEventArgs e)
    {
        VtableOrIndexesButton.Content = (string)VtableOrIndexesButton.Content == "vtable" ? "indexes" : "vtable";
    }

    private void LeftTextBox_OnTextInput(object sender, TextChangedEventArgs e)
    {
        Convert();
    }

    private void LeftRightBoxesScrollPreview(object sender, MouseWheelEventArgs e)
    {
        HorizontalScrollShift(sender, e);
    }
    private bool Sent;
    private void LeftRightBoxesScroll(object sender, ScrollChangedEventArgs e)
    {
        // TODO: fix this, it still triggers them both for some reason
        // TODO: add a toggle for horizontal scrolling, it's sort of annoying sometimes
        if (Sent)
            return;

        Sent = true;
        if (((ScrollViewer)sender).Name == "LeftScrollViewer")
        {
            RightScrollViewer.ScrollToHorizontalOffset(Math.Min(LeftScrollViewer.HorizontalOffset, RightScrollViewer.ScrollableWidth));
            //use inOutMap to scroll the right box to have the line at the top that the left box's line at the top is mapped to

            // get scroll amount of left box
            double scrollAmount = LeftScrollViewer.VerticalOffset;
            // get line height of left box
            double charHeight = LeftTextBox.GetRectFromCharacterIndex(0).Height;
            // get line number of left box's top line
            int lineNumber = (int)(scrollAmount / charHeight);
            double remainder = scrollAmount % charHeight;
            // get line number of right box's top line
            int rightLineNumber = inOutMap[lineNumber];
            double final = rightLineNumber * charHeight;
            if (lineNumber + 1 < inOutMap.Length && inOutMap[lineNumber] != inOutMap[lineNumber + 1])
                final += remainder;
            // TODO: scroll less based on the amount of lines that match to the same line on the right (a multiplier on the remainder?)
            // set scroll amount of right box
            RightScrollViewer.ScrollToVerticalOffset(final);
        } else
        {
            LeftScrollViewer.ScrollToHorizontalOffset(Math.Min(RightScrollViewer.HorizontalOffset, LeftScrollViewer.ScrollableWidth));

            double scrollAmount = RightScrollViewer.VerticalOffset;
            double charHeight = RightTextBox.GetRectFromCharacterIndex(0).Height;
            int lineNumber = (int)(scrollAmount / charHeight);
            double remainder = scrollAmount % charHeight;
            int leftLineNumber = Array.FindIndex(inOutMap, v => v == lineNumber);
            double final = leftLineNumber * charHeight + remainder;
            // always add the remainder because scrolling on the right will always scroll on the left as well
            // TODO: scroll more based on the amount of lines to skip (a multiplier on the remainder?)
            LeftScrollViewer.ScrollToVerticalOffset(final);
        }
        Sent = false;
    }

    private void HorizontalScrollShift(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Shift)
            return;
            
        e.Handled = true;

        Sent = true;
        if (e.Delta < 0)
        {
            ((ScrollViewer)sender).LineRight();
            ((ScrollViewer)sender).LineRight();
            ((ScrollViewer)sender).LineRight();
        }
        else
        {
            ((ScrollViewer)sender).LineLeft();
            ((ScrollViewer)sender).LineLeft();
            ((ScrollViewer)sender).LineLeft();
        }
        Sent = false;
    }
}