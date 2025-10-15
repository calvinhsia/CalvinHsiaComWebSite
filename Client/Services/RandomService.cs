using System;

namespace WordScapeBlazorWasm.Services
{
    /// <summary>
    /// ?? Centralized Random service for reproducible debugging
    /// Provides a single source of truth for all random number generation
    /// 
    /// CRITICAL: Uses lazy initialization to ensure debug mode from URL is applied
    /// before creating the Random instance
    /// </summary>
    public class RandomService
    {
        private Random? _random; // Nullable for lazy initialization
        private bool? _isDebugMode; // Track what mode the current Random was created with
        private readonly string _instanceId; // ?? Unique ID to track this service instance
        private int _callCount = 0; // ?? Track how many times GetRandom is called
        private readonly object _lock = new object(); // Thread safety for lazy init
        
        public RandomService()
        {
            _instanceId = Guid.NewGuid().ToString().Substring(0, 8); // Short unique ID
            // DON'T create Random here - wait until GetRandom() is called
            // This allows debug mode from URL to be set first
            var message = $"?? RandomService created [ID:{_instanceId}] - Random will be lazy-initialized on first use";
            Console.WriteLine(message);
            DebugHelper.Log(message);
        }
        
        /// <summary>
        /// Get the shared Random instance (lazy-initialized on first call)
        /// </summary>
        public Random GetRandom()
        {
            lock (_lock)
            {
                _callCount++;
                
                // Check if we need to create or recreate the Random instance
                var currentDebugMode = DebugHelper.IsDebugEnabled;
                
                if (_random == null || _isDebugMode != currentDebugMode)
                {
                    // Create new Random with appropriate seed
                    var oldRandomId = _random?.GetHashCode().ToString("X8") ?? "none";
                    _random = currentDebugMode ? new Random(1) : new Random();
                    _isDebugMode = currentDebugMode;
                    var newRandomId = _random.GetHashCode().ToString("X8");
                    
                    var message = $"?? GetRandom() call #{_callCount} [ServiceID:{_instanceId}] - Created NEW Random [RandomID:{newRandomId}] (was: {oldRandomId}), Debug: {currentDebugMode}, Seed: {(currentDebugMode ? "1 (fixed)" : "random")}";
                    Console.WriteLine(message);
                    DebugHelper.Log(message);
                }
                else
                {
                    var randomId = _random.GetHashCode().ToString("X8");
                    var message = $"?? GetRandom() call #{_callCount} [ServiceID:{_instanceId}] returning existing Random [RandomID:{randomId}]";
                    Console.WriteLine(message);
                    DebugHelper.Log(message);
                }
                
                return _random;
            }
        }
        
        /// <summary>
        /// Reset the random seed (when debug mode changes)
        /// Forces recreation of Random instance on next GetRandom() call
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                var oldRandomId = _random?.GetHashCode().ToString("X8") ?? "none";
                
                // CRITICAL FIX: Clear everything to force recreation on next GetRandom()
                // This ensures the new Random uses the current DebugHelper.IsDebugEnabled state
                _random = null;
                _isDebugMode = null;
                
                var message = $"?? RandomService.Reset() [ServiceID:{_instanceId}] - Cleared Random [was: RandomID:{oldRandomId}], will recreate on next GetRandom() with current debug mode";
                Console.WriteLine(message);
                DebugHelper.Log(message);
            }
        }
        
        /// <summary>
        /// Get description of current state for debugging
        /// </summary>
        public string GetStateDescription()
        {
            lock (_lock)
            {
                if (_random == null)
                {
                    return $"RandomService [ServiceID:{_instanceId}] - Not yet initialized (lazy init), Calls: {_callCount}";
                }
                
                var randomId = _random.GetHashCode().ToString("X8");
                return $"RandomService [ServiceID:{_instanceId}] [RandomID:{randomId}] - Debug: {_isDebugMode}, Seed: {(_isDebugMode == true ? "1 (fixed)" : "random")}, Calls: {_callCount}";
            }
        }
    }
}
