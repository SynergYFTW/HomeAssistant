using Luna.Gpio.Controllers;
using Luna.Gpio.Exceptions;
using Luna.Logging;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using static Luna.Gpio.Enums;

namespace Luna.Gpio.Drivers {
	internal class SystemDeviceDriver : GpioControllerDriver {
		private GpioController DriverController;

		public SystemDeviceDriver(InternalLogger logger, PinsWrapper pins, PinConfig pinConfig, NumberingScheme scheme) : base(logger, pins, Enums.GpioDriver.SystemDevicesDriver, pinConfig, scheme) {	}

		internal override GpioControllerDriver Init() {
			if (!GpioCore.IsAllowedToExecute) {
				throw new DriverInitializationFailedException(nameof(RaspberryIODriver), "Not allowed to initialize.");
			}

			DriverController = new GpioController(PinNumberingScheme.Logical, new RaspberryPi3Driver());
			return this;
		}

		private void ClosePin(int pinNumber) {
			if (DriverController == null) {
				return;
			}

			if (PinController.IsValidPin(pinNumber) && DriverController.IsPinOpen(pinNumber)) {
				DriverController.ClosePin(pinNumber);
			}
		}

		internal override Pin GetPinConfig(int pinNumber) {
			if (!PinController.IsValidPin(pinNumber) || DriverController == null || !IsDriverInitialized) {
				return new Pin();
			}

			if (DriverController == null) {
				return new Pin();
			}

			try {
				if (!DriverController.IsPinOpen(pinNumber)) {
					DriverController.OpenPin(pinNumber);
				}

				if (!DriverController.IsPinOpen(pinNumber)) {
					return new Pin();
				}

				PinValue value = DriverController.Read(pinNumber);
				PinMode mode = DriverController.GetPinMode(pinNumber);
				Pin config = new Pin(pinNumber, value == PinValue.High ? GpioPinState.Off : GpioPinState.On, mode == PinMode.Input ? GpioPinMode.Input : GpioPinMode.Output);
				return config;
			}
			finally {
				ClosePin(pinNumber);
			}
		}

		internal override bool SetGpioValue(int pin, GpioPinMode mode) {
			if (!PinController.IsValidPin(pin) || !IsDriverInitialized) {
				return false;
			}

			try {
				if (DriverController == null) {
					return false;
				}

				if (!DriverController.IsPinModeSupported(pin, (PinMode) mode)) {
					return false;
				}

				if (!DriverController.IsPinOpen(pin)) {
					DriverController.OpenPin(pin);
				}

				if (!DriverController.IsPinOpen(pin)) {
					return false;
				}

				DriverController.SetPinMode(pin, (PinMode) mode);
				return true;
			}
			finally {
				ClosePin(pin);
			}
		}

		internal override bool SetGpioValue(int pin, GpioPinMode mode, GpioPinState state) {
			if (!PinController.IsValidPin(pin) || !IsDriverInitialized) {
				return false;
			}

			try {
				if (DriverController == null) {
					return false;
				}

				if (!DriverController.IsPinModeSupported(pin, (PinMode) mode)) {
					return false;
				}

				if (!DriverController.IsPinOpen(pin)) {
					DriverController.OpenPin(pin);
				}

				if (!DriverController.IsPinOpen(pin)) {
					return false;
				}

				DriverController.SetPinMode(pin, (PinMode) mode);
				DriverController.Write(pin, state == GpioPinState.Off ? PinValue.High : PinValue.Low);
				return true;
			}
			finally {
				ClosePin(pin);
			}
		}

		internal override GpioPinState GpioPinStateRead(int pin) {
			if (!PinController.IsValidPin(pin) || !IsDriverInitialized) {
				return GpioPinState.Off;
			}

			try {
				if (DriverController == null) {
					return GpioPinState.Off;
				}

				if (!DriverController.IsPinOpen(pin)) {
					DriverController.OpenPin(pin);
				}

				if (!DriverController.IsPinOpen(pin)) {
					return GpioPinState.Off;
				}

				return DriverController.Read(pin) == PinValue.High ? GpioPinState.Off : GpioPinState.On;
			}
			finally {
				ClosePin(pin);
			}
		}

		internal override bool GpioDigitalRead(int pin) {
			if (!PinController.IsValidPin(pin) || !IsDriverInitialized) {
				return false;
			}

			try {
				if (DriverController == null) {
					return false;
				}

				if (!DriverController.IsPinOpen(pin)) {
					DriverController.OpenPin(pin);
				}

				if (!DriverController.IsPinOpen(pin)) {
					return false;
				}

				return !(DriverController.Read(pin) == PinValue.High);
			}
			finally {
				ClosePin(pin);
			}
		}

		internal override bool SetGpioValue(int pin, GpioPinState state) {
			if (!PinController.IsValidPin(pin) || !IsDriverInitialized) {
				return false;
			}

			try {
				if (DriverController == null) {
					return false;
				}

				if (!DriverController.IsPinOpen(pin)) {
					DriverController.OpenPin(pin);
				}

				if (!DriverController.IsPinOpen(pin)) {
					return false;
				}

				DriverController.Write(pin, state == GpioPinState.Off ? PinValue.High : PinValue.Low);
				return true;
			}
			finally {
				ClosePin(pin);
			}
		}

		internal override int GpioPhysicalPinNumber(int bcmPin) {
			if (!PinController.IsValidPin(bcmPin) || !IsDriverInitialized) {
				return -1;
			}

			try {
				if (DriverController == null) {
					return -1;
				}

				if (!DriverController.IsPinOpen(bcmPin)) {
					DriverController.OpenPin(bcmPin);
				}

				if (!DriverController.IsPinOpen(bcmPin)) {
					return -1;
				}

				return -1;
			}
			finally {
				ClosePin(bcmPin);
			}
		}
	}
}
