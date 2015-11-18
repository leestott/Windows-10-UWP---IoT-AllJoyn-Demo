namespace LightbulbService
{
  using com.taulty;
  using com.taulty.lightbulb;
  using System;
  using System.Threading.Tasks;
  using Windows.Devices.AllJoyn;
  using Windows.Devices.Gpio;
  using Windows.Foundation;

  class GpioLightbulbService : IlightbulbService
  {
    public GpioLightbulbService(int gpioPin, string locationName)
    {
      this.gpioPin = gpioPin;
      this.locationName = locationName;

      gpioController = new Lazy<GpioController>(() =>
      {
        return (GpioController.GetDefault());
      });
      ledPin = new Lazy<GpioPin>(() =>
      {
        var pin = gpioController.Value.OpenPin(this.gpioPin);
        pin.SetDriveMode(GpioPinDriveMode.Output);
        return (pin);
      });
    }
    public IAsyncOperation<lightbulbSwitchResult> SwitchAsync(AllJoynMessageInfo info, 
      bool switchOn)
    {
      return (this.SwitchAsync(switchOn).AsAsyncOperation());
    }
    async Task<lightbulbSwitchResult> SwitchAsync(bool switchOn)
    {
      ledPin.Value.Write(switchOn ? GpioPinValue.Low : GpioPinValue.High);
      this.on = switchOn;

      return (lightbulbSwitchResult.CreateSuccessResult());
    }
    public IAsyncOperation<lightbulbGetLocationResult> GetLocationAsync(AllJoynMessageInfo info)
    {
      return (this.LocationAsync().AsAsyncOperation());
    }
    public IAsyncOperation<lightbulbGetStatusResult> GetStatusAsync(AllJoynMessageInfo info)
    {
      return (this.StatusAsync().AsAsyncOperation());
    }
    async Task<lightbulbGetLocationResult> LocationAsync()
    {
      return (lightbulbGetLocationResult.CreateSuccessResult(this.locationName));
    }
    async Task<lightbulbGetStatusResult> StatusAsync()
    {
      return (lightbulbGetStatusResult.CreateSuccessResult(this.on));
    }
    int gpioPin;
    string locationName;
    Lazy<GpioController> gpioController;
    Lazy<GpioPin> ledPin;
    bool on;
  }
}