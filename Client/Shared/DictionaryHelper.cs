using Microsoft.JSInterop;

namespace WordScapeBlazorWasm.Shared
{
    /// <summary>
    /// Shared utility for opening dictionary definitions across all word games
    /// 
    /// Usage Examples:
    /// 1. Basic usage:
    ///    await DictionaryHelper.OpenDictionaryDefinitionAsync(JSRuntime, "word");
    /// 
    /// 2. With custom dictionary:
    ///    await DictionaryHelper.OpenDictionaryDefinitionAsync(JSRuntime, "word", DictionaryHelper.DictionaryUrls.Cambridge);
    /// 
    /// 3. In Razor markup for clickable words:
    ///    &lt;div @onclick="@(() => DictionaryHelper.OpenDictionaryDefinitionAsync(JSRuntime, word))" 
    ///         title="@DictionaryHelper.GetDictionaryClickTooltip(word)"
    ///         style="cursor: pointer;"&gt;@word&lt;/div&gt;
    /// </summary>
    public static class DictionaryHelper
    {
        /// <summary>
        /// Opens a dictionary definition for the specified word in a new tab/window
        /// </summary>
        /// <param name="jsRuntime">JavaScript runtime for browser interop</param>
        /// <param name="word">The word to look up</param>
        /// <param name="dictionaryUrl">Optional custom dictionary URL template. Defaults to Merriam-Webster</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task OpenDictionaryDefinitionAsync(IJSRuntime jsRuntime, string word, string? dictionaryUrl = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(word) || word.Length < 2)
                {
                    return;
                }

                // Use provided URL template or default to Merriam-Webster
                var urlTemplate = dictionaryUrl ?? DictionaryUrls.MerriamWebster;
                var url = string.Format(urlTemplate, word.ToLower().Trim());

                await jsRuntime.InvokeVoidAsync("openUrl", url);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - dictionary lookup is a nice-to-have feature
                Console.WriteLine($"Error opening dictionary definition for '{word}': {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the tooltip text for a clickable word that opens dictionary definitions
        /// </summary>
        /// <param name="word">The word</param>
        /// <param name="additionalInfo">Additional information to include in tooltip</param>
        /// <returns>Tooltip text</returns>
        public static string GetDictionaryClickTooltip(string word, string? additionalInfo = null)
        {
            var baseTooltip = $"Click to look up '{word}' in dictionary";
            
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                return $"{baseTooltip} - {additionalInfo}";
            }
            
            return baseTooltip;
        }

        /// <summary>
        /// Alternative dictionary URLs that can be used with OpenDictionaryDefinitionAsync.
        /// Use {0} as placeholder for the word to look up.
        /// </summary>
        public static class DictionaryUrls
        {
            /// <summary>Merriam-Webster Dictionary (default)</summary>
            public const string MerriamWebster = "https://www.merriam-webster.com/dictionary/{0}";
            
            /// <summary>Dictionary.com</summary>
            public const string Dictionary = "https://www.dictionary.com/browse/{0}";
            
            /// <summary>Cambridge Dictionary</summary>
            public const string Cambridge = "https://dictionary.cambridge.org/dictionary/english/{0}";
            
            /// <summary>Oxford Learner's Dictionary</summary>
            public const string Oxford = "https://www.oxfordlearnersdictionaries.com/definition/english/{0}";
            
            /// <summary>WordReference Dictionary</summary>
            public const string WordReference = "https://www.wordreference.com/definition/{0}";
        }
    }
}