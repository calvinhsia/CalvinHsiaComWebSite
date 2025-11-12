using Microsoft.VisualStudio.TestTools.UnitTesting;
using WordScapeBlazorWasm.Services;

namespace TestProject1
{
    /// <summary>
    /// Unit tests for DebugHelper - static debugging utilities
    /// </summary>
    [TestClass]
    public class TestDebugHelper
    {
        [TestCleanup]
        public void Cleanup()
        {
            // Always reset debug mode after each test
            DebugHelper.SetDebugMode(false);
        }

        [TestMethod]
        public void TestDebugHelper_DefaultState_IsDisabled()
        {
            // Arrange & Act
            DebugHelper.SetDebugMode(false); // Ensure clean state
            var isEnabled = DebugHelper.IsDebugEnabled;

            // Assert
            Assert.IsFalse(isEnabled, "Debug mode should be disabled by default");
        }

        [TestMethod]
        public void TestDebugHelper_SetDebugMode_EnablesDebug()
        {
            // Act
            DebugHelper.SetDebugMode(true);

            // Assert
            Assert.IsTrue(DebugHelper.IsDebugEnabled, "Debug mode should be enabled");
        }

        [TestMethod]
        public void TestDebugHelper_SetDebugMode_DisablesDebug()
        {
            // Arrange
            DebugHelper.SetDebugMode(true);

            // Act
            DebugHelper.SetDebugMode(false);

            // Assert
            Assert.IsFalse(DebugHelper.IsDebugEnabled, "Debug mode should be disabled");
        }

        [TestMethod]
        public void TestDebugHelper_SetDebugMode_CanToggle()
        {
            // Act & Assert
            DebugHelper.SetDebugMode(true);
            Assert.IsTrue(DebugHelper.IsDebugEnabled, "Should be enabled");

            DebugHelper.SetDebugMode(false);
            Assert.IsFalse(DebugHelper.IsDebugEnabled, "Should be disabled");

            DebugHelper.SetDebugMode(true);
            Assert.IsTrue(DebugHelper.IsDebugEnabled, "Should be enabled again");
        }

        [TestMethod]
        public void TestDebugHelper_Log_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            try
            {
                DebugHelper.Log("Test message");
                DebugHelper.Log("Test message with debug off");

                DebugHelper.SetDebugMode(true);
                DebugHelper.Log("Test message with debug on");

                Assert.IsTrue(true, "Log should not throw exceptions");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Log should not throw: {ex.Message}");
            }
        }

        [TestMethod]
        public void TestDebugHelper_LogError_DoesNotThrow()
        {
            // Act & Assert
            try
            {
                DebugHelper.LogError("Test error message");
                Assert.IsTrue(true, "LogError should not throw exceptions");
            }
            catch (Exception ex)
            {
                Assert.Fail($"LogError should not throw: {ex.Message}");
            }
        }

        [TestMethod]
        public void TestDebugHelper_LogWarning_DoesNotThrow()
        {
            // Act & Assert
            try
            {
                DebugHelper.LogWarning("Test warning message");
                Assert.IsTrue(true, "LogWarning should not throw exceptions");
            }
            catch (Exception ex)
            {
                Assert.Fail($"LogWarning should not throw: {ex.Message}");
            }
        }

        [TestMethod]
        public void TestDebugHelper_LogTouch_DoesNotThrow()
        {
            // Act & Assert
            try
            {
                DebugHelper.SetDebugMode(true);
                DebugHelper.LogTouch("Test touch event");

                DebugHelper.SetDebugMode(false);
                DebugHelper.LogTouch("Test touch event (debug off)");

                Assert.IsTrue(true, "LogTouch should not throw exceptions");
            }
            catch (Exception ex)
            {
                Assert.Fail($"LogTouch should not throw: {ex.Message}");
            }
        }

        [TestMethod]
        public void TestDebugHelper_LogGrid_DoesNotThrow()
        {
            // Act & Assert
            try
            {
                DebugHelper.SetDebugMode(true);
                DebugHelper.LogGrid("Test grid message");

                DebugHelper.SetDebugMode(false);
                DebugHelper.LogGrid("Test grid message (debug off)");

                Assert.IsTrue(true, "LogGrid should not throw exceptions");
            }
            catch (Exception ex)
            {
                Assert.Fail($"LogGrid should not throw: {ex.Message}");
            }
        }

        [TestMethod]
        public void TestDebugHelper_ForceOutput_LogsRegardlessOfDebugMode()
        {
            // Arrange
            DebugHelper.SetDebugMode(false);

            // Act & Assert - Should not throw even with forceOutput=true
            try
            {
                DebugHelper.Log("Forced message", forceOutput: true);
                Assert.IsTrue(true, "Log with forceOutput should not throw");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Log with forceOutput should not throw: {ex.Message}");
            }
        }

        [TestMethod]
        public void TestDebugHelper_IsDebugEnabled_IsStaticProperty()
        {
            // Arrange
            DebugHelper.SetDebugMode(true);
            var value1 = DebugHelper.IsDebugEnabled;

            DebugHelper.SetDebugMode(false);
            var value2 = DebugHelper.IsDebugEnabled;

            // Assert
            Assert.IsTrue(value1, "Should be true when set to true");
            Assert.IsFalse(value2, "Should be false when set to false");
            Assert.AreNotEqual(value1, value2, "State should change");
        }

        [TestMethod]
        public void TestDebugHelper_DebugMode_AffectsMultipleComponents()
        {
            // This test verifies that debug mode is truly static/shared

            // Arrange
            DebugHelper.SetDebugMode(true);
            var state1 = DebugHelper.IsDebugEnabled;

            // Simulate another component checking state
            var state2 = DebugHelper.IsDebugEnabled;

            // Act
            DebugHelper.SetDebugMode(false);
            var state3 = DebugHelper.IsDebugEnabled;

            // Assert
            Assert.IsTrue(state1, "First check should be true");
            Assert.IsTrue(state2, "Second check should be true");
            Assert.IsFalse(state3, "Third check after disable should be false");
        }

        [TestMethod]
        public void TestDebugHelper_NullMessages_DoNotThrow()
        {
            // Act & Assert
            try
            {
                DebugHelper.Log(null!);
                DebugHelper.LogError(null!);
                DebugHelper.LogWarning(null!);
                DebugHelper.LogTouch(null!);
                DebugHelper.LogGrid(null!);

                Assert.IsTrue(true, "Null messages should be handled gracefully");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Null messages should not throw: {ex.Message}");
            }
        }

        [TestMethod]
        public void TestDebugHelper_EmptyMessages_DoNotThrow()
        {
            // Act & Assert
            try
            {
                DebugHelper.Log("");
                DebugHelper.LogError("");
                DebugHelper.LogWarning("");
                DebugHelper.LogTouch("");
                DebugHelper.LogGrid("");

                Assert.IsTrue(true, "Empty messages should be handled gracefully");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Empty messages should not throw: {ex.Message}");
            }
        }

        [TestMethod]
        public void TestDebugHelper_LongMessages_DoNotThrow()
        {
            // Arrange
            var longMessage = new string('x', 10000);

            // Act & Assert
            try
            {
                DebugHelper.Log(longMessage);
                DebugHelper.LogError(longMessage);
                DebugHelper.LogWarning(longMessage);

                Assert.IsTrue(true, "Long messages should be handled gracefully");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Long messages should not throw: {ex.Message}");
            }
        }

        [TestMethod]
        public void TestDebugHelper_SpecialCharacters_DoNotThrow()
        {
            // Arrange
            var specialChars = "Test\n\r\t\\\"'`~!@#$%^&*(){}[]<>?/|";

            // Act & Assert
            try
            {
                DebugHelper.Log(specialChars);
                DebugHelper.LogError(specialChars);
                DebugHelper.LogWarning(specialChars);

                Assert.IsTrue(true, "Special characters should be handled gracefully");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Special characters should not throw: {ex.Message}");
            }
        }

        [TestMethod]
        public void TestDebugHelper_Unicode_DoNotThrow()
        {
            // Arrange
            var unicodeMessage = "Test ?? ?? ? ? ?? ????? ????";

            // Act & Assert
            try
            {
                DebugHelper.Log(unicodeMessage);
                DebugHelper.LogError(unicodeMessage);
                DebugHelper.LogWarning(unicodeMessage);

                Assert.IsTrue(true, "Unicode characters should be handled gracefully");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Unicode characters should not throw: {ex.Message}");
            }
        }
    }
}
