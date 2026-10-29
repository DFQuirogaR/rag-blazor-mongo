# Implementation Plan

## Overview

This plan implements the fix for the sidebar collapse bug where the visual state does not update when the toggle button is clicked. The fix addresses missing CSS properties (base width rule and transition) and CSS code cleanup.

## Task Dependency Graph

```json
{
  "tasks": {
    "1": ["3"],
    "2": ["3"],
    "3": ["3.1", "3.2", "3.3"],
    "3.1": ["3.4", "3.5"],
    "3.2": ["3.4", "3.5"],
    "3.3": ["3.4", "3.5"],
    "3.4": ["4"],
    "3.5": ["4"]
  },
  "waves": [
    ["1", "2"],
    ["3"],
    ["3.1", "3.2", "3.3"],
    ["3.4", "3.5"],
    ["4"]
  ]
}
```

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Sidebar Visual Update on Toggle
  - **IMPORTANT**: Write this test BEFORE implementing the fix
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: For deterministic bugs, scope the property to concrete failing cases
  - Test that sidebar width does not change when toggle is clicked (from Bug Condition in design)
  - Run test on UNFIXED code - expect FAILURE (this confirms the bug exists)
  - Document counterexamples found to understand root cause
  - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Navigation Functionality
  - **IMPORTANT**: Follow observation-first methodology
  - Observe: navigation links work correctly, icons visible in collapsed state, text visible in expanded state
  - Write property-based tests capturing observed behavior patterns from Preservation Requirements
  - Property-based testing generates many test cases for stronger guarantees
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - _Requirements: 3.1, 3.2, 3.3_

- [x] 3. Fix for sidebar visual update on toggle

  - [x] 3.1 Update NavMenu.razor.css with base width rule and transition property
    - Add explicit `width: 250px;` to base `.sidebar-container` class (establishes expanded state)
    - Add `transition: width 0.3s ease, background-color 0.3s ease;` to `.sidebar-container` (enables smooth animation)
    - Add `overflow-y: auto;` and `white-space: nowrap;` to `.sidebar-container` (better layout handling)
    - Remove unused `.sidebar-expanded` and `.sidebar-collapsed` classes (cleanup)
    - _Bug_Condition: isBugCondition(input) where userClicksToggle() AND collapsedStateChanges(input) AND (sidebarWidthDoesNotChange() OR backgroundColorDoesNotChange())_
    - _Expected_Behavior: expectedBehavior(result) from design - sidebar width changes from 250px to 70px with smooth transition_
    - _Preservation: Preservation Requirements from design - navigation links, icons, text labels remain functional_
    - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.2, 3.3_

  - [x] 3.2 Clean up duplicate and commented CSS code
    - Remove duplicate `.nav-link` definitions (keep only one clean version)
    - Remove duplicate `.menu-btn` definitions (keep only one clean version)
    - Remove commented CSS blocks (keep only the active rules)
    - Ensure consistent formatting and remove conflicts
    - _Preservation: Preservation Requirements from design - cleanup should not affect functionality_
    - _Requirements: 3.1, 3.2, 3.3_

  - [x] 3.3 Verify MainLayout.razor allows sidebar to adapt
    - Check that `.sidebar` div does not have fixed width constraints
    - Ensure child `.sidebar-container` can control its own width
    - Verify flex properties allow proper sizing if needed
    - _Preservation: Preservation Requirements from design - layout context should not constrain sidebar_
    - _Requirements: 3.1, 3.2, 3.3_

  - [x] 3.4 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Sidebar Visual Update on Toggle
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** - Navigation Functionality
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix (no regressions)
    - _Requirements: 3.1, 3.2, 3.3_

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- **Exploration First**: Tasks 1 and 2 must be completed before task 3 to establish baseline understanding
- **Preservation Testing**: Property-based tests recommended for stronger guarantees on preservation
- **Visual Verification**: After fix, manually verify width changes from 250px to 70px with smooth transition
- **No Regressions**: Tasks 3.4 and 3.5 ensure the fix doesn't break existing functionality