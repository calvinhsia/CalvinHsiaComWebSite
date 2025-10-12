# ?? Quick Reference Card

## Instant Start (Copy-Paste)

```bash
dotnet test --filter "QuickStart_InteractiveHtmlTest"
```

That's it! Opens browser with interactive test page.

---

## Three Simple Options

### 1. ? Quick Test (No Server)
```bash
dotnet test --filter "QuickStart_InteractiveHtmlTest"
```
- Opens browser immediately
- Edit HTML file, refresh to see changes
- File: `TestProject1/bin/Debug/net8.0/quick-test.html`

### 2. ?? Advanced HTML Test (No Server)
```bash
dotnet test --filter "CreateInteractiveHtmlTestHarness"
```
- More features and controls
- Edit HTML file, refresh to see changes
- File: `TestProject1/bin/Debug/net8.0/interactive-test-harness.html`

### 3. ?? Test with Real App (Needs Server)
```bash
# Terminal 1:
cd Client && dotnet run

# Terminal 2:
dotnet test --filter "CreateIframeTestHarness"
```
- Shows your actual Blazor app
- Live CSS/JS injection
- File: `TestProject1/bin/Debug/net8.0/iframe-test-harness.html`

---

## Edit and Experiment

1. Run a test (creates HTML file)
2. Open the HTML file in your editor
3. Modify CSS/JavaScript
4. Save and refresh browser
5. See changes instantly!

---

## Files to Edit

Generated HTML files are in:
```
TestProject1/bin/Debug/net8.0/
??? quick-test.html                  ? Edit this to experiment
??? interactive-test-harness.html
??? iframe-test-harness.html
```

---

## Need Help?

- **Quick Start:** `TestProject1/QUICKSTART.md`
- **Full Docs:** `TestProject1/README_TEST_HARNESS.md`
- **Summary:** `TestProject1/SUMMARY.md`

---

**Pro Tip:** Start with `QuickStart_InteractiveHtmlTest` - it's the simplest and requires no setup!
