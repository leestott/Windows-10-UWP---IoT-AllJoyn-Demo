namespace LightbulbService
{
  using com.taulty.lightbulb;
  using System;
  using System.Threading.Tasks;
  using Windows.Devices.AllJoyn;
  using Windows.Foundation;
  using Windows.System.Profile;
  using Windows.UI;
  using Windows.UI.Core;
  using Windows.UI.Xaml;
  using Windows.UI.Xaml.Controls;
  using Windows.UI.Xaml.Media;

  public sealed partial class MainControl : UserControl, IlightbulbService
  {
    public MainControl()
    {
      this.InitializeComponent();
      this.Loaded += OnLoaded;
    }
    bool IsIoT
    {
      get
      {
        return (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.IoT");
      }
    }
    async void AutomaticallyAdvertiseOnWindowsIoT()
    {
      // set this up to automatically add itself as being 'kitchen' if
      // we are on IoT and 10 seconds elapse.
      if (this.IsIoT)
      {
        for (int i = 5; i > 0; i--)
        {
          await Task.Delay(1000);
          this.btnAdvertise.Content = $"advertising as {this.txtRoom.Text} in {i} seconds";
        }
        this.btnAdvertise.Content = "advertised";
        this.OnAdvertise(null, null);
      }
    }
    void OnAdvertise(object sender, RoutedEventArgs args)
    {
      if (!this.advertised)
      {
        this.advertised = true;

        IlightbulbService service =
          !this.IsIoT ? 
            (IlightbulbService)this : 
            new GpioLightbulbService(GPIO_LED_PIN, this.txtRoom.Text);

        AllJoynBusAttachment busAttachment = new AllJoynBusAttachment();
        lightbulbProducer producer = new lightbulbProducer(busAttachment);
        producer.Service = service;
        producer.Start();
      }
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
      this.AutomaticallyAdvertiseOnWindowsIoT();
    }
    public IAsyncOperation<lightbulbGetLocationResult> GetLocationAsync(AllJoynMessageInfo info)
    {
      return (LocationAsync().AsAsyncOperation());
    }
    async Task<lightbulbGetLocationResult> LocationAsync()
    {
      string location = string.Empty;

      await this.Dispatcher.RunAsync(CoreDispatcherPriority.High,
        () =>
        {
          location = this.txtRoom.Text;
        });
      return (lightbulbGetLocationResult.CreateSuccessResult(location));
    }
    public IAsyncOperation<lightbulbGetStatusResult> GetStatusAsync(AllJoynMessageInfo info)
    {
      return (StatusAsync().AsAsyncOperation());
    }
    async Task<lightbulbGetStatusResult> StatusAsync()
    {
      return (lightbulbGetStatusResult.CreateSuccessResult(this.isOn));
    }
    public IAsyncOperation<lightbulbSwitchResult> SwitchAsync(
      AllJoynMessageInfo info, bool interface_on)
    {
      this.isOn = interface_on;
      return (SwitchAsync(interface_on).AsAsyncOperation());
    }    
    async Task<lightbulbSwitchResult> SwitchAsync(bool on)
    {
      await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
        () =>
        {
          this.bulb.Fill = new SolidColorBrush(on ? Colors.Yellow : Colors.White);
        }
      );
      return (lightbulbSwitchResult.CreateSuccessResult());
    }
    bool advertised;
    bool isOn;
    static readonly int GPIO_LED_PIN = 12;
  }
}
