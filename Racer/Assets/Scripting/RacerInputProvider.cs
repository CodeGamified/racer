// Copyright CodeGamified 2025-2026
// MIT License — Racer
using UnityEngine;
using UnityEngine.InputSystem;

namespace Racer.Scripting
{
    /// <summary>
    /// Captures input for Racer.
    /// Encodes as float for bytecode: steering (left/right) and throttle.
    /// 0=none, 1=up, 2=down, 3=left, 4=right.
    /// Mainly for manual testing — the AI code drives via set_throttle/set_steering.
    /// </summary>
    public class RacerInputProvider : MonoBehaviour
    {
        public static RacerInputProvider Instance { get; private set; }

        public const float INPUT_NONE  = 0f;
        public const float INPUT_UP    = 1f;
        public const float INPUT_DOWN  = 2f;
        public const float INPUT_LEFT  = 3f;
        public const float INPUT_RIGHT = 4f;

        public float CurrentInput { get; private set; }

        private InputAction _upAction;
        private InputAction _downAction;
        private InputAction _leftAction;
        private InputAction _rightAction;

        private void Awake()
        {
            Instance = this;

            _upAction = new InputAction("Up", InputActionType.Button);
            _upAction.AddBinding("<Keyboard>/w");
            _upAction.AddBinding("<Keyboard>/upArrow");
            _upAction.Enable();

            _downAction = new InputAction("Down", InputActionType.Button);
            _downAction.AddBinding("<Keyboard>/s");
            _downAction.AddBinding("<Keyboard>/downArrow");
            _downAction.Enable();

            _leftAction = new InputAction("Left", InputActionType.Button);
            _leftAction.AddBinding("<Keyboard>/a");
            _leftAction.AddBinding("<Keyboard>/leftArrow");
            _leftAction.Enable();

            _rightAction = new InputAction("Right", InputActionType.Button);
            _rightAction.AddBinding("<Keyboard>/d");
            _rightAction.AddBinding("<Keyboard>/rightArrow");
            _rightAction.Enable();
        }

        private void Update()
        {
            if (_upAction.IsPressed())
                CurrentInput = INPUT_UP;
            else if (_downAction.IsPressed())
                CurrentInput = INPUT_DOWN;
            else if (_leftAction.IsPressed())
                CurrentInput = INPUT_LEFT;
            else if (_rightAction.IsPressed())
                CurrentInput = INPUT_RIGHT;
            else
                CurrentInput = INPUT_NONE;
        }

        private void OnDestroy()
        {
            _upAction?.Disable();    _upAction?.Dispose();
            _downAction?.Disable();  _downAction?.Dispose();
            _leftAction?.Disable();  _leftAction?.Dispose();
            _rightAction?.Disable(); _rightAction?.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
