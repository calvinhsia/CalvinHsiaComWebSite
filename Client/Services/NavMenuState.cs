namespace Client.Services
{
    /// <summary>
    /// Shared state so MainLayout's top-bar hamburger can toggle NavMenu on wide pages.
    /// </summary>
    public class NavMenuState
    {
        public bool IsCollapsed { get; private set; } = true;
        public event Action? OnChange;

        public void Toggle()
        {
            IsCollapsed = !IsCollapsed;
            OnChange?.Invoke();
        }

        public void Collapse()
        {
            if (!IsCollapsed)
            {
                IsCollapsed = true;
                OnChange?.Invoke();
            }
        }

        public void SetCollapsed(bool collapsed)
        {
            if (IsCollapsed != collapsed)
            {
                IsCollapsed = collapsed;
                OnChange?.Invoke();
            }
        }
    }
}
