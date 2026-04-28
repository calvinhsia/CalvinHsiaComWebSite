namespace Client.Services
{
    /// <summary>
    /// Tracks the resolved identity role of the signed-in user so that
    /// NavMenu and other components can gate UI without re-running Graph calls.
    /// </summary>
    public enum UserRole { Anonymous, Guest, Owner }

    public class UserContextService
    {
        public UserRole Role { get; private set; } = UserRole.Anonymous;
        public string Email { get; private set; } = string.Empty;

        public bool IsOwner => Role == UserRole.Owner;
        public bool IsAuthenticated => Role != UserRole.Anonymous;

        public event Action? OnChange;

        public void SetUser(string email, UserRole role)
        {
            Email = email;
            Role = role;
            OnChange?.Invoke();
        }

        public void Clear()
        {
            Email = string.Empty;
            Role = UserRole.Anonymous;
            OnChange?.Invoke();
        }
    }
}
