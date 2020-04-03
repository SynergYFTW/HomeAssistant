using Assistant.Extensions;
using Assistant.Gpio.Config;
using Assistant.Gpio.Controllers;
using Assistant.Gpio.Drivers;
using Assistant.Gpio.Events.EventArgs;
using Assistant.Logging.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Assistant.Gpio.Enums;
using static Assistant.Logging.Enums;

namespace Assistant.Gpio.Events {
	internal struct GeneratedValue {
		internal GpioPinState PinState { get; private set; }
		internal bool DigitalValue { get; private set; }

		internal GeneratedValue(GpioPinState _state, bool _digitalValue) {
			PinState = _state;
			DigitalValue = _digitalValue;
		}

		internal void SetState(GpioPinState _state) {
			PinState = _state;
		}

		internal void SetDigitalValue(bool _digitalValue) {
			DigitalValue = _digitalValue;
		}

		internal void Set(GpioPinState _state, bool _digitalValue) {
			PinState = _state;
			DigitalValue = _digitalValue;
		}
	}

	internal class Generator {
		private const int POLL_DELAY = 1; // in ms
		private static IGpioControllerDriver? Driver => PinController.GetDriver();
		private readonly ILogger Logger;
		private bool OverridePolling;
		private readonly GeneratedValue PreviousValue = new GeneratedValue();
		private readonly SemaphoreSlim Sync = new SemaphoreSlim(1, 1);

		internal readonly EventConfig Config;
		private bool IsAvailable => !Config.IsEventRegistered && Driver != null && Driver.IsDriverInitialized;

		internal Generator(EventConfig _config, ILogger _logger) {
			Logger = _logger;
			Config = _config;
			Init();
		}

		internal void OverrideGeneration() => OverridePolling = true;

		private void Init() {
			if(Driver == null) {
				Logger.Warning("Driver isn't started yet.");
				return;
			}

			if (!IsAvailable) {
				Logger.Log("An error occured. Check if the specified pin is valid.", LogLevels.Warn);
				return;
			}

			if (Config.PinMode == GpioPinMode.Alt01 || Config.PinMode == GpioPinMode.Alt02) {
				Logger.Log("Currently only Output/Input polling is supported.", LogLevels.Warn);
				return;
			}

			if (!Driver.SetGpioValue(Config.GpioPin, Config.PinMode)) {
				Logger.Error("Failed to set pin value.");
				return;
			}

			SetInitalValue();
			Helpers.InBackgroundThread(async () => await PollAsync().ConfigureAwait(false), true);
		}

		private async Task PollAsync() {
			if (!IsAvailable) {
				Logger.Log("An error occured. Check if the specified pin is valid.", LogLevels.Warn);
				return;
			}

			Config.SetEventRegisteredStatus(true);
			await Sync.WaitAsync().ConfigureAwait(false);
			Logger.Log($"Started '{(Config.PinMode == GpioPinMode.Input ? "Input" : "Output")}' pin polling for {Config.GpioPin}.", LogLevels.Trace);

			try {
				do {
					bool currentValue = Driver.GpioDigitalRead(Config.GpioPin);
					GpioPinState currentState = currentValue ? GpioPinState.Off : GpioPinState.On;
					OnValueReceived(currentValue, currentState);
					await Task.Delay(POLL_DELAY);
				} while (!OverridePolling);
			}
			finally {
				Config.SetEventRegisteredStatus(false);
				Logger.Log($"Polling for '{Config.GpioPin}' has been stopped.", LogLevels.Trace);
				Sync.Release();
			}
		}

		private void OnValueReceived(bool currentValue, GpioPinState currentState) {
			if (!IsAvailable) {
				return;
			}
			
			switch (Config.PinEventState) {
				case GpioPinEventStates.ON when currentState == GpioPinState.On && PreviousValue.PinState != currentState:
				case GpioPinEventStates.OFF when currentState == GpioPinState.Off && PreviousValue.PinState != currentState:
				case GpioPinEventStates.ALL when PreviousValue.PinState != currentState:
					OnValueChangedEventArgs eventArgs = new OnValueChangedEventArgs(Config.GpioPin, currentState, currentValue, Config.PinMode, PreviousValue.PinState, PreviousValue.DigitalValue);					
					Pin pinConfig = Driver.GetPinConfig(Config.GpioPin);

					switch (Config.Type) {
						case SensorType.IRSensor:
							InvokeOnAllOfType(pinConfig, eventArgs);
							break;
						case SensorType.Relay:
							InvokeOnAllOfType(pinConfig, eventArgs);
							break;
						case SensorType.SoundSensor:
							InvokeOnAllOfType(pinConfig, eventArgs);
							break;
						case SensorType.Buzzer:
							InvokeOnAllOfType(pinConfig, eventArgs);
							break;
						default:
							// TODO: Implement functionality to dynamically handle other sensors
							break;
					}
					
					break;
				case GpioPinEventStates.NONE:
					OverrideGeneration();
					Logger.Log($"Stopping event polling for pin -> {Config.GpioPin} ...", LogLevels.Trace);
					break;
				default:
					break;
			}

			PreviousValue.Set(currentState, currentValue);
		}

		private void InvokeOnAllOfType(Pin pin, OnValueChangedEventArgs args) {
			if(pin.PinMap.Count <= 0) {
				return;
			}

			List<PinMap> maps = pin.GetMapsOfType(Config.Type);

			for(int i = 0; i < maps.Count(); i++) {
				PinMap map = maps[i];

				switch (map.MapEvent) {					
					case MappingEvent.OnActivated when args.CurrentState == GpioPinState.On:
						map.OnFired.Invoke(args);
						break;
					case MappingEvent.OnDeactivated when args.CurrentState == GpioPinState.Off:
						map.OnFired.Invoke(args);
						break;
					default:
					case MappingEvent.Both:
						map.OnFired.Invoke(args);
						break;
				}				
			}
		}

		private void SetInitalValue() {
			if (!IsAvailable) {
				Logger.Log("An error occured. Check if the specified pin is valid.", LogLevels.Warn);
				return;
			}

			if (Config.PinMode == GpioPinMode.Output) {
				Driver.SetGpioValue(Config.GpioPin, GpioPinState.Off);
			}

			PreviousValue.Set(GpioPinState.Off, true);
			Logger.Trace($"Initial pin event values has been set for {Config.GpioPin} pin.");
		}
	}
}
