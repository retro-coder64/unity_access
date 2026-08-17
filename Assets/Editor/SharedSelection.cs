using System;
using UnityEngine;

namespace UnityAccess
{
    /// <summary>
    /// Publishes hierarchy selections without coupling the hierarchy to the inspector.
    /// </summary>
    public static class SharedSelection
    {
        private static GameObject currentObject;

        /// <summary>
        /// Raised whenever the hierarchy commits an object selection.
        /// </summary>
        public static event Action<GameObject> SelectionChanged;

        /// <summary>
        /// Raised when an accessible window requests focus to return to the hierarchy.
        /// </summary>
        public static event Action ReturnToHierarchyRequested;

        /// <summary>
        /// Gets the last object committed by the accessible hierarchy.
        /// </summary>
        public static GameObject CurrentObject
        {
            get { return currentObject; }
        }

        /// <summary>
        /// Stores and publishes an accessible hierarchy selection.
        /// </summary>
        public static void Select(GameObject selectedObject)
        {
            if (selectedObject == null)
            {
                throw new ArgumentNullException(nameof(selectedObject));
            }

            currentObject = selectedObject;
            Action<GameObject> handler = SelectionChanged;
            if (handler != null)
            {
                handler(selectedObject);
            }
        }

        /// <summary>
        /// Publishes a hierarchy-return request without directly coupling editor windows.
        /// </summary>
        public static void RequestHierarchyReturn()
        {
            Action handler = ReturnToHierarchyRequested;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
