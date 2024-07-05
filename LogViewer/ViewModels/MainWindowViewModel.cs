using System.Collections.ObjectModel;
using System;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;
using System.ComponentModel;

namespace LogViewer.ViewModels
{
  public partial class MainWindowViewModel : ViewModelBase
  {
    [ObservableProperty]
    private string windowTitle;

    public ObservableCollection<LogFileDescription> LogFiles { get; }
    
    [ObservableProperty]
    private LogFileDescription? selectedLogFile;

    /*
    public LogFileDescription? SelectedLogFile
    {
      get => selectedLogFile;
      set
      {
        selectedLogFile = value;
        if (selectedLogFile?.ActionType == LogFileDescriptionActionType.OpenFileFromDialog)
          OpenFilePickerAsync();
      }
    }*/



    public MainWindowViewModel()
    {
      WindowTitle = "DirectumLogViewer";

      LogFiles = new ObservableCollection<LogFileDescription>
      {
        new LogFileDescription("Open from file..", LogFileDescriptionActionType.OpenFileFromDialog)
      };

      OnPropertyChanged
    }


    private async Task<IStorageFile?> OpenFilePickerAsync()
    {
      if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
          desktop.MainWindow?.StorageProvider is not { } provider)
        throw new NullReferenceException("Missing StorageProvider instance.");

      var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions()
      {
        Title = "Open Text File",
        AllowMultiple = false
      });

      if (files?.Count >= 1)
        WindowTitle = files[0].Path.LocalPath;

      return files?.Count >= 1 ? files[0] : null;
    }

  }
}
