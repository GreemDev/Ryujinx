using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using static SDL2.SDL;

namespace Ryujinx.Input.SDL2
{
    internal class SDL2JoyConPair : IGamepad
    {
        private IGamepad _left;
        private IGamepad _right;

        private StandardControllerInputConfig _configuration;
        private readonly StickInputId[] _stickUserMapping =
        [
            StickInputId.Unbound,
            StickInputId.Left,
            StickInputId.Right
        ];
        private readonly record struct ButtonMappingEntry(GamepadButtonInputId To, GamepadButtonInputId From)
        {
            public bool IsValid => To is not GamepadButtonInputId.Unbound && From is not GamepadButtonInputId.Unbound;
        }

        private readonly List<ButtonMappingEntry> _buttonsUserMapping = new(20);

        private readonly Lock _userMappingLock = new();

        public GamepadFeaturesFlag Features => (_left?.Features ?? GamepadFeaturesFlag.None) | (_right?.Features ?? GamepadFeaturesFlag.None);

        public string Id => "JoyConPair";

        public string Name => "Nintendo Switch Joy-Con (L/R)";
        private const string _leftName = "Nintendo Switch Joy-Con (L)";
        private const string _rightName = "Nintendo Switch Joy-Con (R)";
        public bool IsConnected => _left is { IsConnected: true } && _right is { IsConnected: true };

        public void Dispose()
        {
            _left?.Dispose();
            _right?.Dispose();
        }
        public GamepadStateSnapshot GetMappedStateSnapshot()
        {
            GamepadStateSnapshot rawState = GetStateSnapshot();
            GamepadStateSnapshot result = default;

            lock (_userMappingLock)
            {
                if (_buttonsUserMapping.Count == 0)
                    return rawState;


                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                foreach (ButtonMappingEntry entry in _buttonsUserMapping)
                {
                    if (!entry.IsValid)
                        continue;

                    // Do not touch state of button already pressed
                    if (!result.IsPressed(entry.To))
                    {
                        result.SetPressed(entry.To, rawState.IsPressed(entry.From));
                    }
                }

                (float leftStickX, float leftStickY) = rawState.GetStick(_stickUserMapping[(int)StickInputId.Left]);
                (float rightStickX, float rightStickY) = rawState.GetStick(_stickUserMapping[(int)StickInputId.Right]);

                result.SetStick(StickInputId.Left, leftStickX, leftStickY);
                result.SetStick(StickInputId.Right, rightStickX, rightStickY);
            }

            return result;
        }

        public Vector3 GetMotionData(MotionInputId inputId)
        {
            return inputId switch
            {
                MotionInputId.SecondAccelerometer => _right.GetMotionData(MotionInputId.Accelerometer),
                MotionInputId.SecondGyroscope => _right.GetMotionData(MotionInputId.Gyroscope),
                _ => _left.GetMotionData(inputId)
            };
        }

        public GamepadStateSnapshot GetStateSnapshot()
        {
            return IGamepad.GetStateSnapshot(this);
        }

        public (float, float) GetStick(StickInputId inputId)
        {
            switch (inputId)
            {
                case StickInputId.Left:
                    {
                        (float x, float y) = _left.GetStick(StickInputId.Left);
                        return (y, -x);
                    }
                case StickInputId.Right:
                    {
                        (float x, float y) = _right.GetStick(StickInputId.Left);
                        return (-y, x);
                    }
                case StickInputId.Unbound:
                case StickInputId.Count:
                default:
                    return (0, 0);
            }
        }

        public bool IsPressed(GamepadButtonInputId inputId)
        {
            return inputId switch
            {
                GamepadButtonInputId.LeftStick => _left.IsPressed(GamepadButtonInputId.LeftStick),
                GamepadButtonInputId.DpadUp => _left.IsPressed(GamepadButtonInputId.Y),
                GamepadButtonInputId.DpadDown => _left.IsPressed(GamepadButtonInputId.A),
                GamepadButtonInputId.DpadLeft => _left.IsPressed(GamepadButtonInputId.B),
                GamepadButtonInputId.DpadRight => _left.IsPressed(GamepadButtonInputId.X),
                GamepadButtonInputId.Minus => _left.IsPressed(GamepadButtonInputId.Start),
                GamepadButtonInputId.LeftShoulder => _left.IsPressed(GamepadButtonInputId.Paddle2),
                GamepadButtonInputId.LeftTrigger => _left.IsPressed(GamepadButtonInputId.Paddle4),
                GamepadButtonInputId.SingleRightTrigger0 => _left.IsPressed(GamepadButtonInputId.LeftShoulder),
                GamepadButtonInputId.SingleLeftTrigger0 => _left.IsPressed(GamepadButtonInputId.RightShoulder),

                GamepadButtonInputId.RightStick => _right.IsPressed(GamepadButtonInputId.LeftStick),
                GamepadButtonInputId.A => _right.IsPressed(GamepadButtonInputId.B),
                GamepadButtonInputId.B => _right.IsPressed(GamepadButtonInputId.Y),
                GamepadButtonInputId.X => _right.IsPressed(GamepadButtonInputId.A),
                GamepadButtonInputId.Y => _right.IsPressed(GamepadButtonInputId.X),
                GamepadButtonInputId.Plus => _right.IsPressed(GamepadButtonInputId.Start),
                GamepadButtonInputId.RightShoulder => _right.IsPressed(GamepadButtonInputId.Paddle1),
                GamepadButtonInputId.RightTrigger => _right.IsPressed(GamepadButtonInputId.Paddle3),
                GamepadButtonInputId.SingleRightTrigger1 => _right.IsPressed(GamepadButtonInputId.LeftShoulder),
                GamepadButtonInputId.SingleLeftTrigger1 => _right.IsPressed(GamepadButtonInputId.RightShoulder),

                _ => false
            };
        }

        public void Rumble(float lowFrequency, float highFrequency, uint durationMs)
        {
            if (lowFrequency != 0)
            {
                _right.Rumble(lowFrequency, lowFrequency, durationMs);
            }
            if (highFrequency != 0)
            {
                _left.Rumble(highFrequency, highFrequency, durationMs);
            }
            if (lowFrequency == 0 && highFrequency == 0)
            {
                _left.Rumble(0, 0, durationMs);
                _right.Rumble(0, 0, durationMs);
            }
        }

        public void SetConfiguration(InputConfig configuration)
        {
            lock (_userMappingLock)
            {

                _configuration = (StandardControllerInputConfig)configuration;
                _left.SetConfiguration(configuration);
                _right.SetConfiguration(configuration);

                _buttonsUserMapping.Clear();

                // First update sticks
                _stickUserMapping[(int)StickInputId.Left] = (StickInputId)_configuration.LeftJoyconStick.Joystick;
                _stickUserMapping[(int)StickInputId.Right] = (StickInputId)_configuration.RightJoyconStick.Joystick;

                // Then left joycon
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftStick, (GamepadButtonInputId)_configuration.LeftJoyconStick.StickButton));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadUp, (GamepadButtonInputId)_configuration.LeftJoycon.DpadUp));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadDown, (GamepadButtonInputId)_configuration.LeftJoycon.DpadDown));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadLeft, (GamepadButtonInputId)_configuration.LeftJoycon.DpadLeft));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadRight, (GamepadButtonInputId)_configuration.LeftJoycon.DpadRight));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Minus, (GamepadButtonInputId)_configuration.LeftJoycon.ButtonMinus));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftShoulder, (GamepadButtonInputId)_configuration.LeftJoycon.ButtonL));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftTrigger, (GamepadButtonInputId)_configuration.LeftJoycon.ButtonZl));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleRightTrigger0, (GamepadButtonInputId)_configuration.LeftJoycon.ButtonSr));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleLeftTrigger0, (GamepadButtonInputId)_configuration.LeftJoycon.ButtonSl));

                // Finally right joycon
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightStick, (GamepadButtonInputId)_configuration.RightJoyconStick.StickButton));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.A, (GamepadButtonInputId)_configuration.RightJoycon.ButtonA));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.B, (GamepadButtonInputId)_configuration.RightJoycon.ButtonB));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.X, (GamepadButtonInputId)_configuration.RightJoycon.ButtonX));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Y, (GamepadButtonInputId)_configuration.RightJoycon.ButtonY));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Plus, (GamepadButtonInputId)_configuration.RightJoycon.ButtonPlus));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightShoulder, (GamepadButtonInputId)_configuration.RightJoycon.ButtonR));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightTrigger, (GamepadButtonInputId)_configuration.RightJoycon.ButtonZr));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleRightTrigger1, (GamepadButtonInputId)_configuration.RightJoycon.ButtonSr));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleLeftTrigger1, (GamepadButtonInputId)_configuration.RightJoycon.ButtonSl));

                SetTriggerThreshold(_configuration.TriggerThreshold);
            }

        }

        public void SetTriggerThreshold(float triggerThreshold)
        {
            _left.SetTriggerThreshold(triggerThreshold);
            _right.SetTriggerThreshold(triggerThreshold);
        }

        public IGamepad GetGamepad(List<string> gamepadsIds)
        {
            this.Dispose();
            var gamepadNames = gamepadsIds.Where(gamepadId => gamepadId != Id)
                .Select((_, index) => SDL_GameControllerNameForIndex(index)).ToList();
            int leftIndex = gamepadNames.IndexOf(_leftName);
            int rightIndex = gamepadNames.IndexOf(_rightName);

            if (leftIndex == -1 || rightIndex == -1)
            {
                return null;
            }

            nint leftGamepadHandle = SDL_GameControllerOpen(leftIndex);
            nint rightGamepadHandle = SDL_GameControllerOpen(rightIndex);
            _left = new SDL2Gamepad(leftGamepadHandle, gamepadsIds[leftIndex]);
            _right = new SDL2Gamepad(rightGamepadHandle, gamepadsIds[leftIndex]);
            return this;
        }
    }
}
