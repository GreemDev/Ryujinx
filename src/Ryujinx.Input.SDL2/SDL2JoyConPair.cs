using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Configuration.Hid.Controller;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using static SDL2.SDL;

namespace Ryujinx.Input.SDL2
{
    internal class SDL2JoyConPair(IGamepad left, IGamepad right) : IGamepad
    {
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

        public GamepadFeaturesFlag Features => (left?.Features ?? GamepadFeaturesFlag.None) |
                                               (right?.Features ?? GamepadFeaturesFlag.None);

        public const string Id = "JoyConPair";
        string IGamepad.Id => Id;

        public string Name => "Nintendo Switch Joy-Con (L/R)";
        private const string LeftName = "Nintendo Switch Joy-Con (L)";
        private const string RightName = "Nintendo Switch Joy-Con (R)";
        public bool IsConnected => left is { IsConnected: true } && right is { IsConnected: true };

        public void Dispose()
        {
            left?.Dispose();
            right?.Dispose();
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
            Vector3 motionData;
            switch (inputId)
            {
                case MotionInputId.Accelerometer:
                case MotionInputId.Gyroscope:
                     motionData = left.GetMotionData(inputId);
                     return new Vector3(-motionData.Z, motionData.Y, motionData.X);
                case MotionInputId.SecondAccelerometer:
                     motionData = right.GetMotionData(MotionInputId.Accelerometer);
                     return new Vector3(motionData.Z, motionData.Y, -motionData.X);
                case MotionInputId.SecondGyroscope:
                     motionData = right.GetMotionData(MotionInputId.Gyroscope);
                     return new Vector3(motionData.Z, motionData.Y, -motionData.X);
                case MotionInputId.Invalid:
                default:
                    return Vector3.Zero;
            }
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
                        (float x, float y) = left.GetStick(StickInputId.Left);
                        return (y, -x);
                    }
                case StickInputId.Right:
                    {
                        (float x, float y) = right.GetStick(StickInputId.Left);
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
                GamepadButtonInputId.LeftStick => left.IsPressed(GamepadButtonInputId.LeftStick),
                GamepadButtonInputId.DpadUp => left.IsPressed(GamepadButtonInputId.Y),
                GamepadButtonInputId.DpadDown => left.IsPressed(GamepadButtonInputId.A),
                GamepadButtonInputId.DpadLeft => left.IsPressed(GamepadButtonInputId.B),
                GamepadButtonInputId.DpadRight => left.IsPressed(GamepadButtonInputId.X),
                GamepadButtonInputId.Minus => left.IsPressed(GamepadButtonInputId.Start),
                GamepadButtonInputId.LeftShoulder => left.IsPressed(GamepadButtonInputId.Paddle2),
                GamepadButtonInputId.LeftTrigger => left.IsPressed(GamepadButtonInputId.Paddle4),
                GamepadButtonInputId.SingleRightTrigger0 => left.IsPressed(GamepadButtonInputId.LeftShoulder),
                GamepadButtonInputId.SingleLeftTrigger0 => left.IsPressed(GamepadButtonInputId.RightShoulder),

                GamepadButtonInputId.RightStick => right.IsPressed(GamepadButtonInputId.LeftStick),
                GamepadButtonInputId.A => right.IsPressed(GamepadButtonInputId.B),
                GamepadButtonInputId.B => right.IsPressed(GamepadButtonInputId.Y),
                GamepadButtonInputId.X => right.IsPressed(GamepadButtonInputId.A),
                GamepadButtonInputId.Y => right.IsPressed(GamepadButtonInputId.X),
                GamepadButtonInputId.Plus => right.IsPressed(GamepadButtonInputId.Start),
                GamepadButtonInputId.RightShoulder => right.IsPressed(GamepadButtonInputId.Paddle1),
                GamepadButtonInputId.RightTrigger => right.IsPressed(GamepadButtonInputId.Paddle3),
                GamepadButtonInputId.SingleRightTrigger1 => right.IsPressed(GamepadButtonInputId.LeftShoulder),
                GamepadButtonInputId.SingleLeftTrigger1 => right.IsPressed(GamepadButtonInputId.RightShoulder),

                _ => false
            };
        }

        public void Rumble(float lowFrequency, float highFrequency, uint durationMs)
        {
            if (lowFrequency != 0)
            {
                right.Rumble(lowFrequency, lowFrequency, durationMs);
            }

            if (highFrequency != 0)
            {
                left.Rumble(highFrequency, highFrequency, durationMs);
            }

            if (lowFrequency == 0 && highFrequency == 0)
            {
                left.Rumble(0, 0, durationMs);
                right.Rumble(0, 0, durationMs);
            }
        }

        public void SetConfiguration(InputConfig configuration)
        {
            lock (_userMappingLock)
            {
                _configuration = (StandardControllerInputConfig)configuration;
                left.SetConfiguration(configuration);
                right.SetConfiguration(configuration);

                _buttonsUserMapping.Clear();

                // First update sticks
                _stickUserMapping[(int)StickInputId.Left] = (StickInputId)_configuration.LeftJoyconStick.Joystick;
                _stickUserMapping[(int)StickInputId.Right] = (StickInputId)_configuration.RightJoyconStick.Joystick;

                // Then left joycon
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftStick,
                    (GamepadButtonInputId)_configuration.LeftJoyconStick.StickButton));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadUp,
                    (GamepadButtonInputId)_configuration.LeftJoycon.DpadUp));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadDown,
                    (GamepadButtonInputId)_configuration.LeftJoycon.DpadDown));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadLeft,
                    (GamepadButtonInputId)_configuration.LeftJoycon.DpadLeft));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.DpadRight,
                    (GamepadButtonInputId)_configuration.LeftJoycon.DpadRight));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Minus,
                    (GamepadButtonInputId)_configuration.LeftJoycon.ButtonMinus));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftShoulder,
                    (GamepadButtonInputId)_configuration.LeftJoycon.ButtonL));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.LeftTrigger,
                    (GamepadButtonInputId)_configuration.LeftJoycon.ButtonZl));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleRightTrigger0,
                    (GamepadButtonInputId)_configuration.LeftJoycon.ButtonSr));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleLeftTrigger0,
                    (GamepadButtonInputId)_configuration.LeftJoycon.ButtonSl));

                // Finally right joycon
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightStick,
                    (GamepadButtonInputId)_configuration.RightJoyconStick.StickButton));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.A,
                    (GamepadButtonInputId)_configuration.RightJoycon.ButtonA));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.B,
                    (GamepadButtonInputId)_configuration.RightJoycon.ButtonB));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.X,
                    (GamepadButtonInputId)_configuration.RightJoycon.ButtonX));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Y,
                    (GamepadButtonInputId)_configuration.RightJoycon.ButtonY));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.Plus,
                    (GamepadButtonInputId)_configuration.RightJoycon.ButtonPlus));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightShoulder,
                    (GamepadButtonInputId)_configuration.RightJoycon.ButtonR));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.RightTrigger,
                    (GamepadButtonInputId)_configuration.RightJoycon.ButtonZr));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleRightTrigger1,
                    (GamepadButtonInputId)_configuration.RightJoycon.ButtonSr));
                _buttonsUserMapping.Add(new ButtonMappingEntry(GamepadButtonInputId.SingleLeftTrigger1,
                    (GamepadButtonInputId)_configuration.RightJoycon.ButtonSl));

                SetTriggerThreshold(_configuration.TriggerThreshold);
            }
        }

        public void SetTriggerThreshold(float triggerThreshold)
        {
            left.SetTriggerThreshold(triggerThreshold);
            right.SetTriggerThreshold(triggerThreshold);
        }

        public static bool IsCombinable(List<string> gamepadsIds)
        {
            (int leftIndex, int rightIndex) = DetectJoyConPair(gamepadsIds);
            return leftIndex >= 0 && rightIndex >= 0;
        }

        private static (int leftIndex, int rightIndex) DetectJoyConPair(List<string> gamepadsIds)
        {
            var gamepadNames = gamepadsIds.Where(gamepadId => gamepadId != Id)
                .Select((_, index) => SDL_GameControllerNameForIndex(index)).ToList();
            int leftIndex = gamepadNames.IndexOf(LeftName);
            int rightIndex = gamepadNames.IndexOf(RightName);

            return (leftIndex, rightIndex);
        }

        public static IGamepad GetGamepad(List<string> gamepadsIds)
        {
            (int leftIndex, int rightIndex) = DetectJoyConPair(gamepadsIds);
            if (leftIndex == -1 || rightIndex == -1)
            {
                return null;
            }

            nint leftGamepadHandle = SDL_GameControllerOpen(leftIndex);
            nint rightGamepadHandle = SDL_GameControllerOpen(rightIndex);

            if (leftGamepadHandle == nint.Zero || rightGamepadHandle == nint.Zero)
            {
                return null;
            }


            return new SDL2JoyConPair(new SDL2Gamepad(leftGamepadHandle, gamepadsIds[leftIndex]),
                new SDL2Gamepad(rightGamepadHandle, gamepadsIds[rightIndex]));
        }
    }
}
