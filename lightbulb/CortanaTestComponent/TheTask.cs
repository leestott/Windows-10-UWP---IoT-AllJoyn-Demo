
namespace CortanaTestComponent
{
  using com.taulty.lightbulb;
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Windows.ApplicationModel.AppService;
  using Windows.ApplicationModel.Background;
  using Windows.ApplicationModel.VoiceCommands;
  using Windows.Devices.AllJoyn;
  using Windows.Storage;

  public sealed class TheTask : IBackgroundTask
  {
    private const int DELAY_TIMEOUT = 10000;
    private const string VOICE_COMMAND_SHOW_LIGHTS = "showLights";
    private const string VOICE_COMMAND_SWITCH_LIGHT = "switchLight";
    private const string VOICE_COMMAND_LOCATION_KEY = "dictatedLocation";
    private const string VOICE_COMMAND_ON_OFF_KEY = "onOffStatus";

    class ServiceInfoWithLocation
    {
      public lightbulbConsumer Consumer { get; set; }
      public string Location { get; set; }
      public bool IsOn { get; set; }
    }
    public async void Run(IBackgroundTaskInstance taskInstance)
    {
      var triggerDetails = taskInstance.TriggerDetails as AppServiceTriggerDetails;

      if (triggerDetails?.Name == "VoiceCommandService")
      {
        var deferral = taskInstance.GetDeferral();
        var cancelledTokenSource = new CancellationTokenSource();

        // Whatever the command is, we need a list of lightbulbs and their
        // location names. Attemping to build that here but trying to make
        // sure that we factor in cancellation.
        taskInstance.Canceled += (s, e) =>
        {
          cancelledTokenSource.Cancel();
        };

        VoiceCommandServiceConnection voiceConnection =
          VoiceCommandServiceConnection.FromAppServiceTriggerDetails(triggerDetails);

        voiceConnection.VoiceCommandCompleted += (s, e) =>
        {
          cancelledTokenSource.Cancel();
        };

        var serviceInfoList = await this.GetLightListingAsync(DELAY_TIMEOUT, cancelledTokenSource.Token);

        await this.ProcessVoiceCommandAsync(serviceInfoList, voiceConnection);

        cancelledTokenSource.Dispose();

        this.StopWatcherAndBusAttachment(serviceInfoList);

        deferral.Complete();
      }
    }
    async Task ProcessVoiceCommandAsync(List<ServiceInfoWithLocation> serviceInfoList,
      VoiceCommandServiceConnection voiceConnection)
    {
      var command = await voiceConnection.GetVoiceCommandAsync();

      switch (command.CommandName)
      {
        case VOICE_COMMAND_SHOW_LIGHTS:
          await ProcessShowLightsCommandAsync(serviceInfoList, voiceConnection);
          break;
        case VOICE_COMMAND_SWITCH_LIGHT:
          await ProcessSwitchLightCommandAsync(serviceInfoList, voiceConnection);
          break;
        default:
          break;
      }
    }
    async Task ProcessSwitchLightCommandAsync(
      List<ServiceInfoWithLocation> serviceInfoList,
      VoiceCommandServiceConnection voiceConnection)
    {
      var message = new VoiceCommandUserMessage();
      var tiles = new List<VoiceCommandContentTile>();
      bool worked = false;

      if ((serviceInfoList == null) || (serviceInfoList.Count == 0))
      {
        message.SpokenMessage = "I couldn't find any lights at all, sorry";
        message.DisplayMessage = "No lights could be found at any location";
      }
      else
      {
        var voiceCommand = await voiceConnection.GetVoiceCommandAsync();

        var location = ExtractPropertyFromVoiceCommand(voiceCommand, VOICE_COMMAND_LOCATION_KEY);
        var onOff = ExtractPropertyFromVoiceCommand(voiceCommand, VOICE_COMMAND_ON_OFF_KEY);

        if (string.IsNullOrEmpty(location))
        {
          message.SpokenMessage = "I couldn't find a location in what you said, sorry";
          message.DisplayMessage = "Interpreted text did not contain an audible location";
        }
        else if (string.IsNullOrEmpty(onOff))
        {
          message.SpokenMessage = "I couldn't figure out whether you said on or off, sorry";
          message.DisplayMessage = "Not clear around on/off status";
        }
        else
        {
          var serviceInfo = serviceInfoList.SingleOrDefault(
            sinfo => string.Compare(sinfo.Location.Trim(), location.Trim(), true) == 0);

          if (serviceInfo == null)
          {
            message.SpokenMessage = $"I couldn't find any lights in the location {location}, sorry";
            message.DisplayMessage = $"No lights in the {location}";
          }
          else
          {
            // It may just work...  
            await serviceInfo.Consumer.SwitchAsync(string.Compare(onOff, "on", true) == 0);

            message.SpokenMessage = $"I think I did it! The light should now be {onOff}";
            message.DisplayMessage = $"the light is now {onOff}";
          }
        }
      }
      var response = VoiceCommandResponse.CreateResponse(message);

      if (worked)
      {
        await voiceConnection.ReportSuccessAsync(response);
      }
      else
      {
        await voiceConnection.ReportFailureAsync(response);
      }
    }
    static string ExtractPropertyFromVoiceCommand(VoiceCommand voiceCommand, string propertyKey)
    {
      string result = string.Empty;

      if (voiceCommand.Properties.ContainsKey(propertyKey))
      {
        var entries = voiceCommand.Properties[propertyKey];

        if ((entries != null) && (entries.Count > 0))
        {
          result = entries[0];
        }
      }
      return(result);
    }

    static async Task ProcessShowLightsCommandAsync(
      List<ServiceInfoWithLocation> serviceInfoList, 
      VoiceCommandServiceConnection voiceConnection)
    {
      var onImageFile = await StorageFile.GetFileFromApplicationUriAsync(
        new Uri("ms-appx:///Assets/Cortana68x68On.png"));
      var offImageFile = await StorageFile.GetFileFromApplicationUriAsync(
        new Uri("ms-appx:///Assets/Cortana68x68Off.png"));

      var message = new VoiceCommandUserMessage();
      var tiles = new List<VoiceCommandContentTile>();

      if ((serviceInfoList == null) || (serviceInfoList.Count == 0))
      {
        message.SpokenMessage = "Either something went wrong, or there are no lights";
        message.DisplayMessage = "I didn't find any lights, sorry";
      }
      else
      {
        message.SpokenMessage = "Yay! I found some lights. Here you go";
        message.DisplayMessage = "Lights found in following places...";

        foreach (var light in serviceInfoList)
        {
          tiles.Add(
            new VoiceCommandContentTile()
            {
              Title = "Light",
              TextLine1 = $"located in {light.Location}",
              ContentTileType = VoiceCommandContentTileType.TitleWith68x68IconAndText,
              Image = light.IsOn ? onImageFile : offImageFile
            });
        }
      }
      var response = VoiceCommandResponse.CreateResponse(message, tiles);

      await voiceConnection.ReportSuccessAsync(response);
    }
    async Task<List<ServiceInfoWithLocation>> GetLightListingAsync(
      int timeoutMs,
      CancellationToken cancelledToken)
    {
      List<ServiceInfoWithLocation> list = null;

      var safeBag = new ConcurrentBag<ServiceInfoWithLocation>();

      this.busAttachment = new AllJoynBusAttachment();

      this.watcher = new lightbulbWatcher(this.busAttachment);

      this.watcher.Added += async (s,e) =>
      {
        var result = await lightbulbConsumer.JoinSessionAsync(e, s);

        if (result.Status == AllJoynStatus.Ok)
        {
          var getLocationResult = await result.Consumer.GetLocationAsync();
          var getStatusResult = await result.Consumer.GetStatusAsync();

          if ((getLocationResult.Status == AllJoynStatus.Ok) &&
            (getStatusResult.Status == AllJoynStatus.Ok))
          {
            safeBag.Add(new ServiceInfoWithLocation()
            {
              Location = getLocationResult.Returnvalue,
              Consumer = result.Consumer,
              IsOn = getStatusResult.Returnvalue
            });
          }
        }
      };
      this.watcher.Start();

      try
      {
        await Task.Delay(timeoutMs, cancelledToken);
        list = new List<ServiceInfoWithLocation>(safeBag.ToArray());
      }
      catch (TaskCanceledException)
      {
        // the list remains null, we got cancelled.
      }
      return (list);
    }
    void StopWatcherAndBusAttachment(List<ServiceInfoWithLocation> serviceInfoList)
    {
      if (serviceInfoList != null)
      {
        foreach (var item in serviceInfoList)
        {
          item.Consumer.Dispose();
        }
      }
      if (this.watcher != null)
      {
        this.watcher.Stop();
        this.watcher.Dispose();
        this.busAttachment.Disconnect();
      }
    }
    lightbulbWatcher watcher;
    AllJoynBusAttachment busAttachment;
  }
}