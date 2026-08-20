using UnityEngine;

namespace UnityAccess
{
    /// <summary>Normalises keyboard commands used by accessible editor controls.</summary>
    public static class AccessibleKeyboard
    {
        public static bool IsConfirm(Event currentEvent)
        {
            return currentEvent != null &&
                (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter);
        }

        public static bool IsCancel(Event currentEvent)
        {
            return currentEvent != null && currentEvent.keyCode == KeyCode.Escape;
        }

        public static bool TryGetVerticalDirection(Event currentEvent, out int direction)
        {
            direction = 0;
            if (currentEvent == null)
            {
                return false;
            }

            if (currentEvent.keyCode == KeyCode.UpArrow)
            {
                direction = -1;
                return true;
            }

            if (currentEvent.keyCode == KeyCode.DownArrow)
            {
                direction = 1;
                return true;
            }

            return false;
        }
    }
}
