using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows;
using Slik.Utils;
using System;
using System.Linq;

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
    

    private void VtableOrIndexesButton_OnClick(object sender, RoutedEventArgs e)
    {
        VtableOrIndexesButton.Content = (string)VtableOrIndexesButton.Content == "vtable" ? "indexes" : "vtable";
        
        LeftTextBox_OnTextInput(null, null);
    }

    private void LeftTextBox_OnTextInput(object sender, TextChangedEventArgs e)
    {
        if (VtableOrIndexesButton == null)
            return; // Ran too quick

        var lines = FunctionParser.ParseInput(LeftTextBox.Text, 
            (string)VtableOrIndexesButton.Content == "vtable" ? 
                FunctionParser.ParseReturnType.Vftable : 
                FunctionParser.ParseReturnType.Indexes);
        
        if (lines == null)
            return;
        
        RightTextBox.Text = lines.Aggregate((a, b) => a + "\n" + b);
    }

    private void LeftRightBoxesScrollPreview(object sender, MouseWheelEventArgs e)
    {
        HorizontalScrollShift(sender, e);
    }
    private int _sent;
    private void LeftRightBoxesScroll(object sender, ScrollChangedEventArgs e)
    {
        //get the stack trace
        StackTrace stackTrace = new();
        Debug.WriteLine(stackTrace.GetFrame(10)!.GetMethod()!.Name);
        string name = ((ScrollViewer)sender).Name;
        if (_sent == 3)
        {
            _sent = 0;
            return;
        }
        if (_sent == 2 && name == "LeftScrollViewer")
        {
            _sent = 0;
            return;
        } else if (_sent == 1 && name == "RightScrollViewer")
        {
            _sent = 0;
            return;
        }

        // TODO: add a toggle for horizontal scrolling, it's sort of annoying sometimes
        
        if (((ScrollViewer)sender).Name == "LeftScrollViewer")
        {
            
            _sent = 1;
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
            
            if (lineNumber > FunctionParser.InOutMap.Length)
                return;
            
            int rightLineNumber = FunctionParser.InOutMap[lineNumber];
            double final = rightLineNumber * charHeight;
            if (lineNumber + 1 < FunctionParser.InOutMap.Length && FunctionParser.InOutMap[lineNumber] != FunctionParser.InOutMap[lineNumber + 1])
                final += remainder;
            // TODO: scroll less based on the amount of lines that match to the same line on the right (a multiplier on the remainder?)
            // set scroll amount of right box
            if (final != 0)
            {
                RightScrollViewer.ScrollToVerticalOffset(final);

            }
        } else
        {
            _sent = 2;
            LeftScrollViewer.ScrollToHorizontalOffset(Math.Min(RightScrollViewer.HorizontalOffset, LeftScrollViewer.ScrollableWidth));

            double scrollAmount = RightScrollViewer.VerticalOffset;
            double charHeight = RightTextBox.GetRectFromCharacterIndex(0).Height;
            int lineNumber = (int)(scrollAmount / charHeight);
            double remainder = scrollAmount % charHeight;
            int leftLineNumber = Array.FindIndex(FunctionParser.InOutMap, v => v == lineNumber);
            double final = leftLineNumber * charHeight + remainder;
            // always add the remainder because scrolling on the right will always scroll on the left as well
            // TODO: scroll more based on the amount of lines to skip (a multiplier on the remainder?)
            LeftScrollViewer.ScrollToVerticalOffset(final);
        }
    }

    private void HorizontalScrollShift(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Shift)
            return;
            
        e.Handled = true;

        if (e.Delta < 0)
        {
            _sent = 3;
            ((ScrollViewer)sender).LineRight();
            _sent = 3;
            ((ScrollViewer)sender).LineRight();
            _sent = 3;
            ((ScrollViewer)sender).LineRight();
        }
        else
        {
            _sent = 3;
            ((ScrollViewer)sender).LineLeft();
            _sent = 3;
            ((ScrollViewer)sender).LineLeft();
            _sent = 3;
            ((ScrollViewer)sender).LineLeft();
        }
    }
}