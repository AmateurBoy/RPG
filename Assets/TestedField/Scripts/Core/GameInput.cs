using UnityEngine;
using UnityEngine.InputSystem;

namespace TestedField
{
    /// <summary>
    /// Действия ввода — аналог IA_* и IMC_Default из UE. Собираются в коде, чтобы не зависеть от ассета.
    /// Клавиши слотов навыков — в SkillConfig.SlotKeys.
    /// </summary>
    public static class GameInput
    {
        static InputActionMap map;

        public static InputAction Move { get; private set; }
        public static InputAction Attack { get; private set; }
        public static InputAction Roll { get; private set; }
        public static InputAction Sprint { get; private set; }
        public static InputAction Zoom { get; private set; }
        public static InputAction Interact { get; private set; }
        public static InputAction Menu { get; private set; }
        public static InputAction Inventory { get; private set; }

        /// <summary>Открыто окно, которое забирает ввод (инвентарь, консоль) — игровые действия не срабатывают.</summary>
        public static bool GameplayBlocked => InventoryOpen || DevConsole.IsOpen;

        public static bool InventoryOpen { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            map?.Dispose();
            map = null;
            InventoryOpen = false;
        }

        public static void Ensure()
        {
            if (map != null)
                return;

            map = new InputActionMap("Gameplay");

            Move = map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            Attack = map.AddAction("Attack", InputActionType.Button);
            Attack.AddBinding("<Mouse>/leftButton");
            Attack.AddBinding("<Gamepad>/buttonWest");

            Roll = map.AddAction("Roll", InputActionType.Button);
            Roll.AddBinding("<Keyboard>/space");
            Roll.AddBinding("<Gamepad>/buttonEast");

            Sprint = map.AddAction("Sprint", InputActionType.Button);
            Sprint.AddBinding("<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            Zoom = map.AddAction("Zoom", InputActionType.Value);
            Zoom.AddBinding("<Mouse>/scroll/y");
            Zoom.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/dpad/down")
                .With("Positive", "<Gamepad>/dpad/up");

            Interact = map.AddAction("Interact", InputActionType.Button);
            Interact.AddBinding("<Keyboard>/e");
            Interact.AddBinding("<Gamepad>/buttonNorth");

            Menu = map.AddAction("Menu", InputActionType.Button);
            Menu.AddBinding("<Keyboard>/escape");
            Menu.AddBinding("<Gamepad>/start");

            Inventory = map.AddAction("Inventory", InputActionType.Button);
            Inventory.AddBinding("<Keyboard>/i");
            Inventory.AddBinding("<Gamepad>/select");

            map.Enable();
        }

        /// <summary>Клавиша слота навыка нажата в этом кадре (аналог WasInputKeyJustPressed).</summary>
        public static bool WasKeyPressedThisFrame(Key key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || key == Key.None)
                return false;
            return keyboard[key].wasPressedThisFrame;
        }
    }
}
