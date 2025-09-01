using DictionaryLib;

namespace WordScapeBlazorWasm.Services
{
    /// <summary>
    /// Singleton service to manage expensive DictionaryLib instances
    /// Only creates one small and one large dictionary for the entire application
    /// </summary>
    public interface IDictionaryService
    {
        DictionaryLib.DictionaryLib SmallDictionary { get; }
        DictionaryLib.DictionaryLib LargeDictionary { get; }
        bool IsWord(string word, DictionaryType type = DictionaryType.Small);
        string GetRandomWord(DictionaryType type = DictionaryType.Small);
        List<string> GenerateSubWords(string word, out int lookupCount, int minLength = 3, int maxSubWords = 1500, DictionaryType type = DictionaryType.Small);
        DictionaryLib.DictionaryLib CreateWithCustomRandom(DictionaryType type, Random random);
    }

    public class DictionaryService : IDictionaryService
    {
        private readonly Lazy<DictionaryLib.DictionaryLib> _smallDictionary;
        private readonly Lazy<DictionaryLib.DictionaryLib> _largeDictionary;

        public DictionaryService()
        {
            DebugHelper.Log("DictionaryService: Initializing lazy dictionary instances...");
            
            // Use Lazy<T> for thread-safe singleton initialization
            _smallDictionary = new Lazy<DictionaryLib.DictionaryLib>(() =>
            {
                DebugHelper.Log("DictionaryService: Creating Small Dictionary instance (expensive operation)...");
                var dict = new DictionaryLib.DictionaryLib(DictionaryType.Small);
                DebugHelper.Log("DictionaryService: Small Dictionary created successfully");
                return dict;
            });

            _largeDictionary = new Lazy<DictionaryLib.DictionaryLib>(() =>
            {
                DebugHelper.Log("DictionaryService: Creating Large Dictionary instance (expensive operation)...");
                var dict = new DictionaryLib.DictionaryLib(DictionaryType.Large);
                DebugHelper.Log("DictionaryService: Large Dictionary created successfully");
                return dict;
            });
        }

        public DictionaryLib.DictionaryLib SmallDictionary => _smallDictionary.Value;
        public DictionaryLib.DictionaryLib LargeDictionary => _largeDictionary.Value;

        public bool IsWord(string word, DictionaryType type = DictionaryType.Small)
        {
            return type == DictionaryType.Small 
                ? SmallDictionary.IsWord(word) 
                : LargeDictionary.IsWord(word);
        }

        public string GetRandomWord(DictionaryType type = DictionaryType.Small)
        {
            return type == DictionaryType.Small 
                ? SmallDictionary.RandomWord() 
                : LargeDictionary.RandomWord();
        }

        public List<string> GenerateSubWords(string word, out int lookupCount, int minLength = 3, int maxSubWords = 1500, DictionaryType type = DictionaryType.Small)
        {
            if (type == DictionaryType.Small)
            {
                return SmallDictionary.GenerateSubWords(word, out lookupCount, MinLength: minLength, MaxSubWords: maxSubWords);
            }
            else
            {
                return LargeDictionary.GenerateSubWords(word, out lookupCount, MinLength: minLength, MaxSubWords: maxSubWords);
            }
        }

        /// <summary>
        /// Create a dictionary instance with custom Random for word generation scenarios
        /// Note: This still creates a new instance but reuses the underlying data
        /// </summary>
        public DictionaryLib.DictionaryLib CreateWithCustomRandom(DictionaryType type, Random random)
        {
            DebugHelper.Log($"DictionaryService: Creating {type} dictionary with custom Random instance");
            return new DictionaryLib.DictionaryLib(type, random);
        }
    }
}