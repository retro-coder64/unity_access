namespace UnityAccess
{
    /// <summary>Owns the common lifecycle of an accessible text edit.</summary>
    public sealed class AccessibleTextEdit
    {
        public bool IsEditing { get; private set; }
        public string Value { get; set; } = string.Empty;

        public void Begin(string value)
        {
            Value = value ?? string.Empty;
            IsEditing = true;
        }

        public void End()
        {
            IsEditing = false;
        }
    }
}
