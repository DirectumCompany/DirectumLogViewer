using System.Collections.ObjectModel;
using System;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LogViewer.ViewModels
{
  public partial class MainWindowViewModel : ViewModelBase
  {
    [ObservableProperty]
    private string windowTitle;

    public ObservableCollection<LogFileDescription> LogFiles { get; }

    private LogFileDescription? selectedLogFile = null;
    public LogFileDescription? SelectedLogFile
    {
      get => selectedLogFile;
      set
      {
        selectedLogFile = value;
        OpenLogFile(selectedLogFile);
      }
    }

    public MainWindowViewModel()
    {
      WindowTitle = "DirectumLogViewer";

      LogFiles = new ObservableCollection<LogFileDescription>
      {
        new LogFileDescription("Open from file..", LogFileDescriptionActionType.OpenFileFromDialog)
      };
    }

    private void OpenLogFile(LogFileDescription? logFile)
    {
      // todo logic here
      WindowTitle = OpenFilePickerAsync().Path.LocalPath;
    }

    private IStorageFile? OpenFilePickerAsync()
    {
      if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
          desktop.MainWindow?.StorageProvider is not { } provider)
        throw new NullReferenceException("Missing StorageProvider instance.");

      var files =  provider.OpenFilePickerAsync(new FilePickerOpenOptions()
      {
        Title = "Open Log File",
        AllowMultiple = false
      }).Result;

      return files?.Count >= 1 ? files[0] : null;
    }

  }
}
