# 04-validate: Final validation — build and test

Run a full solution build and the test suite to confirm everything is functional after the upgrade. Fix any remaining test failures or build warnings in projects touched by this upgrade.

Run `dotnet build` on the solution, then `dotnet test --filter "TestCategory!=Manual"` to execute all automated tests (unit, integration, interactive) while excluding manual tests that require human interaction.

**Done when**: Solution builds without errors or warnings (in modified projects), all automated tests pass, and no manual suppressions remain.
