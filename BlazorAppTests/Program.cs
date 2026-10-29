using BlazorAppTests;

// Run preservation tests
var test = new NavMenuPreservationTests();

Console.WriteLine("=== Running Preservation Property Tests ===\n");
Console.WriteLine("Verifying that sidebar collapse fix does NOT break existing functionality.\n");

var results = new Dictionary<string, bool>
{
    { "Navigation links", test.VerifyNavigationLinks() },
    { "Icons in collapsed state", test.VerifyIconsVisibleInCollapsedState() },
    { "Text in expanded state", test.VerifyTextVisibleInExpandedState() },
    { "Hover effects", test.VerifyHoverEffects() },
    { "Active link highlighting", test.VerifyActiveLinkHighlighting() },
    { "Sidebar toggle functionality", test.VerifySidebarToggleFunctionality() },
    { "No regressions", test.VerifyNoRegressions() }
};

Console.WriteLine("\n=== Final Summary ===");
int passed = 0, failed = 0;
foreach (var result in results)
{
    Console.WriteLine($"{result.Key}: {(result.Value ? "✅ PASS" : "❌ FAIL")}");
    if (result.Value) passed++;
    else failed++;
}

Console.WriteLine($"\nTotal: {passed} passed, {failed} failed");

if (failed == 0)
{
    Console.WriteLine("\n✅ ALL PRESERVATION TESTS PASSED: The sidebar collapse fix does not break existing functionality.");
    Environment.Exit(0);
}
else
{
    Console.WriteLine($"\n❌ {failed} PRESERVATION TEST(S) FAILED: The fix may have introduced regressions.");
    Environment.Exit(1);
}
