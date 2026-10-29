# Sidebar Collapse Fix Design

## Overview

The bug manifests when the user clicks the toggle button - the `collapsed` state changes internally (visible in the text display) but the visual appearance of the sidebar does not update. The CSS rules for width and background color exist but are not being applied correctly to the rendered HTML element.

The issue is likely caused by one or more of:
1. CSS specificity conflicts preventing the `.collapsed` class rules from taking precedence
2. Missing CSS properties needed for smooth transitions
3. Layout context issues where parent containers constrain the sidebar width
4. CSS rules that are malformed or not properly structured for Blazor component rendering

This design documents the root cause analysis and specific changes needed to fix the visual update issue while preserving existing functionality.

## Glossary

- **Bug_Condition (C)**: The condition that triggers the bug - when the `collapsed` boolean state changes but the sidebar's visual width and background color do not update
- **Property (P)**: The desired behavior when toggle is clicked - the sidebar width should transition smoothly between 250px (expanded) and 70px (collapsed), and the background should change as defined
- **Preservation**: Existing navigation functionality, link visibility, and icon display that must remain unchanged by the fix
- **`NavMenu.razor`**: The Blazor component containing the sidebar markup and toggle logic
- **`NavMenu.razor.css`**: The CSS file containing styling rules for the sidebar
- **`collapsed` state**: Boolean property in `NavMenu` that determines whether sidebar is expanded (false) or collapsed (true)

## Bug Details

### Bug Condition

The bug manifests when the user clicks the toggle button in the `NavMenu` component. The `collapsed` boolean field toggles correctly (as evidenced by the text display changing), but the `sidebar-container` div does not visually update to reflect the new state. The CSS classes `.sidebar-expanded` and `.sidebar-collapsed` exist, but the applied class `sidebar-container collapsed` does not result in the expected visual changes.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type UserInteraction (toggle button click)
  OUTPUT: boolean
  
  RETURN userClicksToggle()
         AND collapsedStateChanges(input)
         AND (sidebarWidthDoesNotChange() OR backgroundColorDoesNotChange())
END FUNCTION
```

### Examples

- **Concrete Example 1**: User clicks toggle button when sidebar is expanded (width: 250px). After click, `collapsed = true` is set, class `sidebar-container collapsed` is applied, but width remains 250px instead of changing to 70px.
- **Concrete Example 2**: User clicks toggle button when sidebar is collapsed (width: 70px). After click, `collapsed = false` is set, class `sidebar-container` (without collapsed) is applied, but width remains 70px instead of expanding to 250px.
- **Concrete Example 3**: Background color is set to red for expanded state and green for collapsed state in CSS, but neither color is visible on the rendered sidebar.
- **Edge Case - CSS Override**: Even with inline styles or `!important` declarations in CSS, the visual appearance does not change, suggesting possible layout context issues.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Navigation links must remain clickable and functional in both states
- Icons must remain visible in collapsed state (text labels should disappear)
- Text labels must remain visible in expanded state (when `!collapsed` condition is met)
- Toggle button must remain responsive and functional
- Page content must continue to render correctly alongside the sidebar

**Scope:**
All inputs that do NOT involve the toggle button should be completely unaffected by this fix. This includes:
- Navigation link clicks (routing should work as before)
- Mouse hover effects on navigation items
- Keyboard navigation (if implemented)
- Responsive behavior on different screen sizes

## Hypothesized Root Cause

Based on the CSS code analysis, the most likely issues are:

1. **CSS Specificity Conflict**: The `.sidebar-container` rule uses `!important` for the background color, but there's no explicit width rule for the expanded state. The `.sidebar-container.collapsed` rule has `!important` but may be overridden by other styles.

2. **Missing Base Width Rule**: The CSS has width rules for collapsed state (70px) but lacks an explicit width rule for the expanded state (250px) in the base `.sidebar-container` class. CSS requires both rules to be present for transitions to work.

3. **Missing Transition Properties**: No `transition` property is defined on `.sidebar-container`, so even if width changes, there would be no smooth animation.

4. **Layout Context Issue**: The parent `.sidebar` div in `MainLayout.razor` may be constraining the sidebar width with its own CSS rules, preventing the child `.sidebar-container` from actually resizing visually.

5. **CSS Structure Issue**: The CSS contains duplicated and commented-out sections that may indicate confusion about which rules should be active. Multiple definitions of `.nav-link` and `.menu-btn` suggest the CSS may not be in a consistent state.

## Correctness Properties

Property 1: Bug Condition - Sidebar Visual Update on Toggle

_For any_ user interaction where the toggle button is clicked, the fixed code SHALL update the visual appearance of the sidebar-container to reflect the collapsed state, including both width (250px for expanded, 70px for collapsed) and background color, with a smooth transition animation.

**Validates: Requirements 2.1, 2.2, 2.3**

Property 2: Preservation - Navigation Functionality

_For any_ input that is NOT the toggle button (navigation link clicks, hover, keyboard events), the fixed code SHALL preserve the original behavior of displaying text labels in expanded state and icons only in collapsed state, with all links remaining functional.

**Validates: Requirements 3.1, 3.2, 3.3**

## Fix Implementation

### Changes Required

**File**: `BlazorApp/Components/Layout/NavMenu.razor.css`

**Specific Changes**:

1. **Add base width rule to `.sidebar-container`**:
   - Add explicit `width: 250px;` to the base `.sidebar-container` class
   - This establishes the expanded state width that the collapsed state overrides

2. **Add transition property for smooth animation**:
   - Add `transition: width 0.3s ease, background-color 0.3s ease;` to `.sidebar-container`
   - This enables smooth 300ms transitions between expanded and collapsed states

3. **Remove redundant and conflicting CSS**:
   - Remove the unused `.sidebar-expanded` and `.sidebar-collapsed` classes
   - Clean up duplicate `.nav-link` and `.menu-btn` definitions
   - Keep only the necessary `.sidebar-container.collapsed` override

4. **Add overflow handling**:
   - Add `overflow-y: auto;` to allow scrolling if menu items exceed viewport height
   - Add `white-space: nowrap;` to prevent text wrapping in collapsed state

**File**: `BlazorApp/Components/Layout/NavMenu.razor` (potentially)

**Specific Changes**:

1. **Add container div for proper layout**:
   - Wrap `.nav-scrollable` content in a scrollable container if needed
   - Ensure the sidebar container has proper dimensions

2. **Verify CSS class application**:
   - The existing `@(collapsed ? "collapsed" : "")` syntax is correct
   - No code changes needed if CSS is fixed properly

**File**: `BlazorApp/Components/Layout/MainLayout.razor` (potentially)

**Specific Changes**:

1. **Check parent `.sidebar` constraints**:
   - Verify that the `.sidebar` div does not have fixed width constraints
   - May need to add CSS to allow child to control its own width

### Detailed CSS Changes

```css
/* BEFORE (in NavMenu.razor.css) */
.sidebar-container {
    width: 250px;
    background: red !important;
}

.sidebar-container.collapsed {
    width: 70px;
    background: green !important;
}

/* AFTER (cleaned up version) */
.sidebar-container {
    width: 250px;
    background: linear-gradient(180deg, #081b4b, #3b0055);
    transition: width 0.3s ease, background-color 0.3s ease;
    overflow-y: auto;
    white-space: nowrap;
}

.sidebar-container.collapsed {
    width: 70px;
}
```

### Implementation Steps

1. **Update NavMenu.razor.css**:
   - Replace the existing `.sidebar-container` rules with the new version
   - Remove or comment out the old `.sidebar-expanded` and `.sidebar-collapsed` classes
   - Clean up duplicate definitions

2. **Verify MainLayout.razor**:
   - Ensure `.sidebar` class doesn't constrain width
   - May need to add `flex: 0 0 auto;` or similar to allow child to control width

3. **Test the fix**:
   - Click toggle button and verify visual width changes
   - Verify background color changes as expected
   - Confirm smooth transition animation occurs
   - Test navigation links remain functional

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, verify the bug exists on the current code, then verify the fix resolves the visual update issue while preserving all existing functionality.

### Exploratory Bug Condition Checking

**Goal**: Confirm the current bug exists before implementing the fix. This establishes a baseline to measure against after the fix.

**Test Plan**: 
1. Run the Blazor app in development mode
2. Open the application in a browser
3. Click the toggle button and observe that the width does not change visually
4. Use browser developer tools to inspect the `.sidebar-container` element and verify the `collapsed` class is being added/removed
5. Check computed CSS styles to see which rules are being applied

**Test Cases**:
1. **Width Change Test**: Click toggle when expanded, verify width doesn't change to 70px (bug present)
2. **Background Color Test**: Click toggle, verify background color doesn't change (bug present)
3. **Class Application Test**: Inspect element, verify `collapsed` class is added/removed on click (state changes correctly)
4. **Transition Absence Test**: Click toggle, verify no smooth animation occurs (transition missing)

**Expected Counterexamples**:
- Computed width remains 250px even when `collapsed` class is applied
- Background color remains unchanged despite CSS rules specifying red/green
- No CSS transition property appears in computed styles

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds (toggle click), the fixed code produces the expected visual updates.

**Pseudocode:**
```
FOR ALL userClicksToggle() DO
  result := toggleSidebar()
  ASSERT sidebarWidthChanges(result)  // Should transition to 70px or 250px
  ASSERT backgroundColorChanges(result)  // Should change to green or original
  ASSERT smoothTransitionOccurred(result)  // Should have 300ms animation
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold (non-toggle interactions), the fixed code preserves the original behavior.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT navigationLinksWork(input)  // Links should remain clickable
  ASSERT iconsVisibleInCollapsed(input)  // Icons should show when collapsed
  ASSERT textVisibleInExpanded(input)  // Text should show when expanded
  ASSERT hoverEffectsWork(input)  // Mouse hover should still function
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain
- It catches edge cases that manual tests might miss
- It provides strong guarantees that behavior is unchanged

**Test Plan**:
1. Observe current behavior on unfixed code for navigation link clicks
2. Write tests verifying links remain functional after fix
3. Verify icon/text visibility logic works in both states
4. Test hover effects and active state styling

### Unit Tests

- **Width Change Test**: Verify `.sidebar-container` width changes from 250px to 70px when `collapsed` class is applied
- **Background Color Test**: Verify background color changes when collapsed class is applied
- **Transition Property Test**: Verify CSS transition property exists on `.sidebar-container`
- **Class Application Test**: Verify `collapsed` class is added/removed on toggle click

### Property-Based Tests

- **Random Toggle Sequence**: Generate random sequences of toggle clicks and verify width updates correctly after each
- **Responsive Width Test**: Test with various viewport widths to ensure the sidebar resizes properly
- **Navigation Stability Test**: Generate random navigation link clicks while toggling sidebar, verify all links remain functional

### Integration Tests

- **Full Toggle Flow Test**: Open app, click toggle multiple times, verify visual updates occur with animation
- **Navigation with Toggle Test**: Toggle sidebar, click navigation links, verify routing works in both states
- **Hover Effect Test**: Hover over navigation items in both expanded and collapsed states, verify hover effects work
- **Responsive Breakpoint Test**: Test sidebar behavior on different screen sizes, verify responsive CSS rules work

### Manual Verification Checklist

After implementing the fix, verify:
- [ ] Toggle button is visible and clickable
- [ ] Clicking toggle changes sidebar width from 250px to 70px (or vice versa)
- [ ] Width change includes smooth 300ms transition animation
- [ ] Background color changes as defined in CSS
- [ ] Icons remain visible in collapsed state
- [ ] Text labels disappear in collapsed state
- [ ] Navigation links are clickable in both states
- [ ] Hover effects work on navigation items
- [ ] Active page link is highlighted correctly
- [ ] Page content renders correctly alongside sidebar
- [ ] No console errors occur when toggling
