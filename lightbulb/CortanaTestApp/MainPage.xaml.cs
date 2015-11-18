namespace CortanaTestApp
{
  using System;
  using System.Runtime.InteropServices;
  using Windows.ApplicationModel;
  using Windows.ApplicationModel.Background;
  using Windows.ApplicationModel.VoiceCommands;
  using Windows.Storage;
  using Windows.UI.Xaml;
  using Windows.UI.Xaml.Controls;

  public sealed partial class MainPage : Page
  {
    public MainPage()
    {
      this.InitializeComponent();
    }
    async void OnRegisterCommands(object sender, RoutedEventArgs e)
    {
      var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(@"ms-appx:///VoiceCommands.xml"));

      await VoiceCommandDefinitionManager.InstallCommandDefinitionsFromStorageFileAsync(file);
    }
  }
}
