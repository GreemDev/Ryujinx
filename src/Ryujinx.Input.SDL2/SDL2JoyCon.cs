using Ryujinx.Common.Configuration.Hid;
using System.Numerics;

namespace Ryujinx.Input.SDL2
{
    internal class SDL2JoyCon : IGamepad
    {
        readonly IGamepad _gamepad;
        private enum JoyConType
        {
            Left , Right
        }

        public const string Prefix = "Nintendo Switch Joy-Con";
        public const string LeftName = "Nintendo Switch Joy-Con (L)";
        public const string RightName = "Nintendo Switch Joy-Con (R)";

        private readonly JoyConType _joyConType;

        public SDL2JoyCon(nint gamepadHandle, string driverId)
        {
            _gamepad = new SDL2Gamepad(gamepadHandle, driverId);
            _joyConType = Name switch
            {
                LeftName => JoyConType.Left,
                RightName => JoyConType.Right,
                _ => JoyConType.Left
            };
        }

        public Vector3 GetMotionData(MotionInputId inputId)
        {
            var motionData = _gamepad.GetMotionData(inputId);
            return _joyConType switch
            {
                JoyConType.Left => new Vector3(-motionData.Z, motionData.Y, motionData.X),
                JoyConType.Right => new Vector3(motionData.Z, motionData.Y, -motionData.X),
                _ => Vector3.Zero
            };
        }

        public void SetTriggerThreshold(float triggerThreshold)
        {
            _gamepad.SetTriggerThreshold(triggerThreshold);
        }

        public void SetConfiguration(InputConfig configuration)
        {
            _gamepad.SetConfiguration(configuration);
        }

        public void Rumble(float lowFrequency, float highFrequency, uint durationMs)
        {
            _gamepad.Rumble(lowFrequency, highFrequency, durationMs);
        }

        public GamepadStateSnapshot GetMappedStateSnapshot()
        {
            return GetStateSnapshot();
        }

        public GamepadStateSnapshot GetStateSnapshot()
        {
            return IGamepad.GetStateSnapshot(this);
        }


        public (float, float) GetStick(StickInputId inputId)
        {
            if (inputId == StickInputId.Left)
            {
                switch (_joyConType)
                {
                    case JoyConType.Left:
                        {
                            (float x, float y) = _gamepad.GetStick(inputId);
                            return (y, -x);
                        }
                    case JoyConType.Right:
                        {
                            (float x, float y) = _gamepad.GetStick(inputId);
                            return (-y, x);
                        }
                }
            } 
            return (0, 0);
        }

        public GamepadFeaturesFlag Features => _gamepad.Features;
        public string Id => _gamepad.Id;
        public string Name => _gamepad.Name;
        public bool IsConnected => _gamepad.IsConnected;

        public bool IsPressed(GamepadButtonInputId inputId)
        {
            return _joyConType switch
            {
                JoyConType.Left => inputId switch
                {
                    GamepadButtonInputId.LeftStick => _gamepad.IsPressed(GamepadButtonInputId.LeftStick),
                    GamepadButtonInputId.DpadUp => _gamepad.IsPressed(GamepadButtonInputId.Y),
                    GamepadButtonInputId.DpadDown => _gamepad.IsPressed(GamepadButtonInputId.A),
                    GamepadButtonInputId.DpadLeft => _gamepad.IsPressed(GamepadButtonInputId.B),
                    GamepadButtonInputId.DpadRight => _gamepad.IsPressed(GamepadButtonInputId.X),
                    GamepadButtonInputId.Minus => _gamepad.IsPressed(GamepadButtonInputId.Start),
                    GamepadButtonInputId.LeftShoulder => _gamepad.IsPressed(GamepadButtonInputId.Paddle2),
                    GamepadButtonInputId.LeftTrigger => _gamepad.IsPressed(GamepadButtonInputId.Paddle4),
                    GamepadButtonInputId.SingleRightTrigger0 => _gamepad.IsPressed(GamepadButtonInputId.RightShoulder),
                    GamepadButtonInputId.SingleLeftTrigger0 => _gamepad.IsPressed(GamepadButtonInputId.LeftShoulder),
                    _ => false
                },
                JoyConType.Right => inputId switch
                {
                    GamepadButtonInputId.RightStick => _gamepad.IsPressed(GamepadButtonInputId.LeftStick),
                    GamepadButtonInputId.A => _gamepad.IsPressed(GamepadButtonInputId.B),
                    GamepadButtonInputId.B => _gamepad.IsPressed(GamepadButtonInputId.Y),
                    GamepadButtonInputId.X => _gamepad.IsPressed(GamepadButtonInputId.A),
                    GamepadButtonInputId.Y => _gamepad.IsPressed(GamepadButtonInputId.X),
                    GamepadButtonInputId.Plus => _gamepad.IsPressed(GamepadButtonInputId.Start),
                    GamepadButtonInputId.RightShoulder => _gamepad.IsPressed(GamepadButtonInputId.Paddle1),
                    GamepadButtonInputId.RightTrigger => _gamepad.IsPressed(GamepadButtonInputId.Paddle3),
                    GamepadButtonInputId.SingleRightTrigger1 => _gamepad.IsPressed(GamepadButtonInputId.RightShoulder),
                    GamepadButtonInputId.SingleLeftTrigger1 => _gamepad.IsPressed(GamepadButtonInputId.LeftShoulder),
                    _ => false
                }
            };
        }

        public void Dispose()
        {
            _gamepad.Dispose();
        }
    }
}
