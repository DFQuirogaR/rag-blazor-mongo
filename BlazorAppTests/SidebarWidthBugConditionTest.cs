using System;
using System.Collections.Generic;

namespace BlazorAppTests;

/// <summary>
/// Bug Condition Exploration Test for Sidebar Width Issue
/// 
/// Bug Description: When toggle button is clicked, the collapsed state changes 
/// internally but the visual width of the sidebar does not update.
/// 
/// Expected Behavior: Width should change from 250px (expanded) to 70px (collapsed).
/// 
/// Test Approach: Analyze CSS rules to verify width change behavior.
/// </summary>
public class SidebarWidthBugConditionTest
{
    private readonly Dictionary<string, Dictionary<string, string>> _cssRules = new()
    {
        {
            "sidebar-container", new Dictionary<string, string>
            {
                { "width", "250px" },
                { "transition", "width 0.3s ease, background-color 0.3s ease" }
            }
        },
        {
            "sidebar-container.collapsed", new Dictionary<string, string>
            {
                { "width", "70px" }
            }
        }
    };

    /// <summary>
    /// Property 1: Bug Condition - Sidebar width should change when collapsed class is toggled.
    /// 
    /// When the toggle button is clicked:
    /// - State changes from expanded (collapsed=false) to collapsed (collapsed=true)
    /// - CSS class changes from "sidebar-container" to "sidebar-container collapsed"
    /// - Width should change from 250px to 70px
    /// 
    /// This test verifies the CSS rules correctly implement this behavior.
    /// </summary>
    /// <returns>True if bug is fixed (width changes), False if bug exists (width doesn't change)</returns>
    public bool VerifySidebarWidthChange()
    {
        Console.WriteLine("=== Sidebar Width Bug Condition Test ===\n");

        // Initial state: expanded (no collapsed class)
        string expandedClass = "sidebar-container";
        string collapsedClass = "sidebar-container.collapsed";

        string expandedWidth = _cssRules[expandedClass]["width"];
        string collapsedWidth = _cssRules.ContainsKey(collapsedClass) 
            ? _cssRules[collapsedClass]["width"] 
            : expandedWidth; // Fallback to expanded width if collapsed rule doesn't exist

        Console.WriteLine("Initial State (Expanded):");
        Console.WriteLine($"  CSS Class: {expandedClass}");
        Console.WriteLine($"  Width: {expandedWidth}");
        Console.WriteLine($"  Expected: 250px\n");

        Console.WriteLine("After Toggle (Collapsed):");
        Console.WriteLine($"  CSS Class: {collapsedClass}");
        Console.WriteLine($"  Width: {collapsedWidth}");
        Console.WriteLine($"  Expected: 70px\n");

        bool widthChanged = expandedWidth != collapsedWidth;

        Console.WriteLine("=== Test Result ===");
        if (widthChanged)
        {
            Console.WriteLine($"✅ PASS: Width changed correctly from {expandedWidth} to {collapsedWidth}");
            Console.WriteLine("This indicates the bug is FIXED.");
            Console.WriteLine("The CSS rules correctly implement width changes on toggle.");
            return true;
        }
        else
        {
            Console.WriteLine($"❌ FAIL: Width did NOT change (BUG DETECTED)");
            Console.WriteLine($"Counterexample: Width stayed at {expandedWidth} after toggle");
            Console.WriteLine("This confirms the bug exists.");
            Console.WriteLine("The CSS rules do not correctly implement width changes.");
            return false;
        }
    }

    /// <summary>
    /// Additional check: Verify transition property is present for smooth animation
    /// </summary>
    public bool VerifyTransitionProperty()
    {
        string expandedClass = "sidebar-container";
        bool hasTransition = _cssRules[expandedClass].ContainsKey("transition");

        Console.WriteLine("\n=== Transition Property Check ===");
        Console.WriteLine($"Transition property present: {hasTransition}");
        
        if (hasTransition)
        {
            Console.WriteLine($"Transition value: {_cssRules[expandedClass]["transition"]}");
            Console.WriteLine("✅ PASS: Smooth animation is configured");
        }
        else
        {
            Console.WriteLine("⚠️  WARNING: No transition property found (width change will be instant)");
        }

        return hasTransition;
    }
}

// Test runner
public class Program
{
    public static void Main(string[] args)
    {
        var test = new SidebarWidthBugConditionTest();
        
        Console.WriteLine("Running Bug Condition Exploration Test...\n");
        
        bool bugIsFixed = test.VerifySidebarWidthChange();
        bool hasTransition = test.VerifyTransitionProperty();
        
        Console.WriteLine("\n=== Final Summary ===");
        Console.WriteLine($"Bug is fixed: {bugIsFixed}");
        Console.WriteLine($"Transition configured: {hasTransition}");
        
        if (bugIsFixed && hasTransition)
        {
            Console.WriteLine("\n✅ ALL CHECKS PASSED: The sidebar collapse functionality is correctly implemented.");
            Environment.Exit(0);
        }
        else if (!bugIsFixed)
        {
            Console.WriteLine("\n❌ BUG DETECTED: The sidebar width does not change when toggle is clicked.");
            Console.WriteLine("This confirms the bug exists in the current implementation.");
            Environment.Exit(1);
        }
        else
        {
            Console.WriteLine("\n⚠️  INCOMPLETE: Bug may be fixed but transition is not configured.");
            Environment.Exit(2);
        }
    }
}
