using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace DungeonExporer.UI
{
    /// <summary>
    /// Ensures the scene <see cref="EventSystem"/> uses <c>InputSystem_Actions</c> UI map bindings.
    /// Level1's serialized <see cref="InputSystemUIInputModule"/> can point at a missing default asset GUID;
    /// without valid Point/Click actions, uGUI buttons never receive clicks.
    /// </summary>
    public static class UiEventSystemBootstrap
    {
        public static InputActionAsset WiredActions { get; private set; }

        public static void EnsureEventSystem(InputActionAsset actions)
        {
            if (actions == null)
            {
                Debug.LogWarning("UiEventSystemBootstrap: InputActionAsset is null; UI clicks will not work.");
                return;
            }

            WiredActions = actions;

            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                eventSystem = go.GetComponent<EventSystem>();
            }

            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
                module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            module.actionsAsset = actions;

            InputActionMap uiMap = actions.FindActionMap("UI", throwIfNotFound: false);
            if (uiMap == null)
            {
                Debug.LogError("UiEventSystemBootstrap: InputActionAsset has no 'UI' action map.");
                return;
            }

            module.point = InputActionReference.Create(uiMap.FindAction("Point", throwIfNotFound: true));
            module.move = InputActionReference.Create(uiMap.FindAction("Navigate", throwIfNotFound: true));
            module.submit = InputActionReference.Create(uiMap.FindAction("Submit", throwIfNotFound: true));
            module.cancel = InputActionReference.Create(uiMap.FindAction("Cancel", throwIfNotFound: true));
            module.leftClick = InputActionReference.Create(uiMap.FindAction("Click", throwIfNotFound: true));
            module.rightClick = InputActionReference.Create(uiMap.FindAction("RightClick", throwIfNotFound: true));
            module.middleClick = InputActionReference.Create(uiMap.FindAction("MiddleClick", throwIfNotFound: true));
            module.scrollWheel = InputActionReference.Create(uiMap.FindAction("ScrollWheel", throwIfNotFound: true));
            module.trackedDevicePosition =
                InputActionReference.Create(uiMap.FindAction("TrackedDevicePosition", throwIfNotFound: true));
            module.trackedDeviceOrientation =
                InputActionReference.Create(uiMap.FindAction("TrackedDeviceOrientation", throwIfNotFound: true));

            module.enabled = true;

            actions.Enable();
            uiMap.Enable();
        }

        public static void SetPlayerMapEnabled(bool enabled)
        {
            if (WiredActions == null)
                return;

            InputActionMap player = WiredActions.FindActionMap("Player", throwIfNotFound: false);
            if (player == null)
                return;

            if (enabled)
                player.Enable();
            else
                player.Disable();
        }
    }
}
