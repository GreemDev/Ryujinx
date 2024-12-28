using LibHac.Common;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryujinx.Input.HLE
{
    public class InputManager(IGamepadDriver keyboardDriver, IGamepadDriver gamepadDriver)
        : IDisposable
    {
        public IGamepadDriver KeyboardDriver { get; } = keyboardDriver;
        public IGamepadDriver GamepadDriver { get; } = gamepadDriver;
        public IGamepadDriver MouseDriver { get; private set; }

        public void SetMouseDriver(IGamepadDriver mouseDriver)
        {
            MouseDriver?.Dispose();

            MouseDriver = mouseDriver;
        }

        public NpadManager CreateNpadManager()
        {
            return new NpadManager(KeyboardDriver, GamepadDriver, MouseDriver);
        }

        public TouchScreenManager CreateTouchScreenManager()
        {
            if (MouseDriver == null)
            {
                throw new InvalidOperationException("Mouse Driver has not been initialized.");
            }

            return new TouchScreenManager(MouseDriver.GetGamepad("0") as IMouse);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                KeyboardDriver?.Dispose();
                GamepadDriver?.Dispose();
                MouseDriver?.Dispose();
            }
        }

        private void removeSDPWhenExternalPadsConnected(List<Tuple<string,string>> availableDevices) {
            //remove all steam virtual gamepads
            availableDevices.RemoveAll(a => a.Item2 == "Steam Virtual Gamepad");
            //remove Steam Deck Controller if external controllers are connected (docked game mode)
            if (availableDevices.Count > 1) {
                var steamDeckPad = availableDevices.FindFirst( a => a.Item2 == "Steam Deck Controller");                
                if (steamDeckPad.HasValue)                {
                    availableDevices.Remove(steamDeckPad.Value);
                }
            }
        }

        private List<Tuple<string,string>> getGamepadsDescriptions() {
            var result = new List<Tuple<string,string>> ();
            foreach (string id in GamepadDriver.GamepadsIds) {
                    result.Add(Tuple.Create(id,GamepadDriver.GetGamepad(id).Name));
            }
            return result;
        }

        private void LinkDevicesToPlayers(List<InputConfig> _inputConfig) {
           var _availableDevices = getGamepadsDescriptions();
            removeSDPWhenExternalPadsConnected(_availableDevices);
            var _playersWithNoDevices = new List<PlayerIndex>();
            //Remove all used Devices in current Config and at the same time list player with missing Devices
            foreach(PlayerIndex _playerId in Enum.GetValues(typeof(PlayerIndex)))
            {
                var _config = _inputConfig.Find(inputConfig => inputConfig.PlayerIndex == _playerId);
                if (_config != null && _config.Backend != InputBackendType.WindowKeyboard)
                {
                    //check device id of the player is in the existing/connected devices
                    var _connectedDevice = _availableDevices.FindFirst(d => d.Item1 == _config.Id);
                    if (_connectedDevice.HasValue)
                    {
                        _availableDevices.Remove(_connectedDevice.Value);
                    }
                    else
                    {
                        _playersWithNoDevices.Add(_playerId);
                    }
                }
            }

            var hasChanges = _playersWithNoDevices.Count() > 0 && _availableDevices.Count() > 0;
            if (hasChanges) 
            {
                Logger.Info?.Print(LogClass.Configuration, $"Controllers configuration changed. Updating players configuration...");
                for (int i = 0; i < _playersWithNoDevices.Count; i++)
                {
                    var _playerId = _playersWithNoDevices[i];
                    var _config = _inputConfig.Find(inputConfig => inputConfig.PlayerIndex == _playerId);
                    if (_config != null && _availableDevices.Count > 0)
                    {
                        var _device = _availableDevices.First();
                        var deviceId = _device.Item1;
                        var deviceName = _device.Item2;
                        _config.Id = _device.Item1;
                        Logger.Info?.Print(LogClass.Configuration, $"Link Player {_playerId} to Device {deviceName}");
                        _availableDevices.Remove(_device);
                    }
                }
                Logger.Info?.Print(LogClass.Configuration, $"Updated players configuration to sync with Controllers configuration changes.");
            }
        }             

        public void AddUpdaterForConfiguration(List<InputConfig> _inputConfig) {
                GamepadDriver.OnGamepadConnected += id => LinkDevicesToPlayers(_inputConfig);
                GamepadDriver.OnGamepadDisconnected += id => LinkDevicesToPlayers(_inputConfig);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }
    }
}
