using LogReader;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LogViewer
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {

    public static readonly string NotificationError = "NotificationError";

    public static readonly string NotificationTypeKey = "Type";

    public static readonly string NotificationFilePathKey = "FilePath";

    public static readonly string NotificationTimeKey = "Time";

    private readonly string OpenAction = "OpenAction";

    private readonly List<LogHandler> LogHandlers = new List<LogHandler>();

    private readonly ObservableCollection<LogLine> logLines = new ObservableCollection<LogLine>();

    private readonly int gridUpdatePeriod = 700;

    private LogWatcher logWatcher;

    private readonly Uri notificationIcon;

    private readonly string iconFileName = "horse.png";

    private ScrollViewer gridScrollViewer;

    public MainWindow()
    {
      InitializeComponent();

      SettingsWindow.Load();

      if (SettingsWindow.IsFirstRun() && !SettingsWindow.ShowSettingsDialog() == true)
        System.Windows.Application.Current.Shutdown();

      notificationIcon = SaveNotifyLogoFromResource();

      var files = FindLogs(SettingsWindow.LogsPath);

      CreateHandlers(files);

      InitControls(files);

      SetNotificationActivated();
    }

    private Uri SaveNotifyLogoFromResource()
    {
      var directory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
      var imageFilePath = System.IO.Path.Combine(directory, iconFileName);

      ImageConverter converter = new ImageConverter();
      var data = (byte[])converter.ConvertTo(Properties.Resources.horse, typeof(byte[]));
      File.WriteAllBytes(imageFilePath, data);

      return new Uri(imageFilePath);
    }

    private string[] FindLogs(string directory)
    {
      string[] allfiles = Directory.GetFiles(directory, "*.log", SearchOption.AllDirectories);

      string machineName = System.Environment.MachineName.ToLower();
      var currentDate = DateTime.Today.ToString("yyyy-MM-dd");

      var whiteList = SettingsWindow.WhitelistLogs.Split(new[] { '\r', '\n' })
        .Select(s => s.Trim().ToLower().Replace("${machinename}", machineName).Replace("${shortdate}", currentDate))
        .Where(s => !String.IsNullOrEmpty(s))
        .ToArray();

      return allfiles.Select(f => new LogFile(f))
        .Where(n => whiteList.Contains(System.IO.Path.GetFileNameWithoutExtension(n.Name.ToLower())) && !n.Name.StartsWith(LogHandler.ConvertedFilePrefix))
        .Select(r => r.FullPath)
        .ToArray();
    }

    private void CreateHandlers(string[] files)
    {
      foreach (var file in files)
        Task.Run(() => LogHandlers.Add(new LogHandler(file, notificationIcon, SettingsWindow.BackgroundConvert)));
    }

    private void InitControls(string[] files)
    {
      LogsFileNames.Items.Clear();

      foreach (var file in files)
        LogsFileNames.Items.Add(new LogFile(file));

      LogsFileNames.Items.Add(new LogFile(OpenAction, "Open from file..."));
    }

    private void SetNotificationActivated()
    {
      ToastNotificationManagerCompat.OnActivated += toastArgs =>
      {
        ToastArguments args = ToastArguments.Parse(toastArgs.Argument);

        var type = string.Empty;
        args.TryGetValue(NotificationTypeKey, out type);

        if (type == NotificationError)
        {
          var filePath = string.Empty;
          args.TryGetValue(NotificationFilePathKey, out filePath);

          var time = string.Empty;
          args.TryGetValue(NotificationTimeKey, out time);

          Application.Current.Dispatcher.Invoke(delegate
          {
            if (!LogsFileNames.IsEnabled)
              return;

            var selectedLog = (LogFile)LogsFileNames.SelectedItem;

            if (selectedLog == null || selectedLog.FullPath.ToLower() != filePath.ToLower())
            {
              var logWithError = LogsFileNames.Items.Cast<LogFile>().FirstOrDefault(i => i.FullPath.ToLower() == filePath.ToLower());

              if (logWithError == null)
                return;

              LogsFileNames.SelectedItem = logWithError;
            }

            var dt = new DateTime(long.Parse(time));
            var itemWithError = logLines.FirstOrDefault(i => i.Level == LogHandler.LogLevelError && i.Time == dt);
            if (itemWithError != null)
            {
              BringToForeground();
              if (!string.IsNullOrEmpty(Filter.Text))
              {
                FilterLines(string.Empty);
                Filter.Text = null;
              }
              LogsGrid.SelectedItem = itemWithError;
              LogsGrid.ScrollIntoView(itemWithError);
            }
          });
        }
      };
    }

    private void CloseLogFile()
    {
      // Clear previous log resources
      if (logWatcher != null)
      {
        logWatcher.Dispose();
        logWatcher = null;
      }

      LogsGrid.ItemsSource = null;
      logLines.Clear();
      GC.Collect();
    }

    private void OpenLogFile(string fullPath)
    {
      try
      {
        LoadBar.Visibility = Visibility.Visible;
        LogsFileNames.IsEnabled = false;
        Filter.IsEnabled = false;
        LogsGrid.IsEnabled = false;

        logWatcher = new LogWatcher(fullPath);
        logWatcher.NewLine += OnNewLine;
        logWatcher.FileReCreated += OnFileReCreated;
        logWatcher.ReadToEndLine();
        LogsGrid.ItemsSource = logLines;
        gridScrollViewer = GetScrollViewer(LogsGrid);
        logWatcher.StartWatch(gridUpdatePeriod);
      }
      catch (Exception e)
      {
        MessageBox.Show($"Error opening log from '{fullPath}'.\n{e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
      finally
      {
        LoadBar.Visibility = Visibility.Hidden;
        LogsFileNames.IsEnabled = true;
        Filter.IsEnabled = true;
        LogsGrid.IsEnabled = true;
        Filter.Text = null;
      }
    }

    private void Files_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      CloseLogFile();

      var comboBox = sender as ComboBox;

      LogFile selectedItem = comboBox.SelectedItem as LogFile;
      if (selectedItem == null)
        return;

      if (selectedItem.FullPath == OpenAction)
      {
        var dialog = new CommonOpenFileDialog
        {
          IsFolderPicker = false
        };

        dialog.Filters.Add(new CommonFileDialogFilter("Log Files (*.log)", ".log"));

        if (CommonFileDialogResult.Ok == dialog.ShowDialog())
        {
          // Создать фоновый обработчик для нового файла.
          LogHandlers.Add(new LogHandler(dialog.FileName, notificationIcon, SettingsWindow.BackgroundConvert));

          var logFile = new LogFile(dialog.FileName);
          comboBox.Items.Insert(comboBox.Items.Count - 1, logFile);
          comboBox.SelectedItem = logFile;
        }
        else
          comboBox.SelectedItem = null;

        return;
      }

      comboBox.Items.Refresh();
      OpenLogFile(selectedItem.FullPath);
    }

    private ScrollViewer GetScrollViewer(UIElement element)
    {
      if (element == null)
        return null;

      ScrollViewer result = null;
      for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element) && result == null; i++)
      {
        if (VisualTreeHelper.GetChild(element, i) is ScrollViewer)
          result = (ScrollViewer)(VisualTreeHelper.GetChild(element, i));
        else
          result = GetScrollViewer(VisualTreeHelper.GetChild(element, i) as UIElement);
      }

      return result;
    }

    private void OnNewLine(string line, bool isEndLine, double process)
    {
      var logLine = Converter.ConvertToObject(line);

      Application.Current.Dispatcher.Invoke(
        new Action(() =>
        {
          if (LoadBar.Visibility == Visibility.Visible && LoadBar.Value != process)
            LoadBar.Dispatcher.Invoke(() => LoadBar.Value = process, DispatcherPriority.Background);

          var scrollToEnd = false;

          if (gridScrollViewer != null)
          {
            gridScrollViewer.UpdateLayout();

            if (gridScrollViewer.VerticalOffset == gridScrollViewer.ScrollableHeight)
              scrollToEnd = true;
          }

          logLines.Add(logLine);

          if (isEndLine)
          {
            if (!string.IsNullOrEmpty(Filter.Text))
              FilterLines(Filter.Text);
          }

          if (scrollToEnd)
            LogsGrid.ScrollIntoView(logLine);
        }));
    }

    private void OnFileReCreated()
    {
      Application.Current.Dispatcher.Invoke(new Action(() =>
      {
        logLines.Clear();
      }));
    }

    private void LogsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var line = (sender as DataGrid).SelectedItem as LogLine;

      if (line == null)
        DetailText.Text = null;
      else
      {
        DetailText.Text = String.Empty;

        if (!String.IsNullOrEmpty(line.UserName))
          DetailText.Text += $"UserName: {line.UserName} \n";

        if (!String.IsNullOrEmpty(line.Tenant))
          DetailText.Text += $"Tenant: {line.Tenant} \n";

        if (!String.IsNullOrEmpty(line.Version))
          DetailText.Text += $"Version: {line.Version} \n";

        if (!String.IsNullOrEmpty(line.FullMessage))
        {
          if (!String.IsNullOrEmpty(DetailText.Text))
            DetailText.Text += "\n";

          DetailText.Text += line.FullMessage;
        }
      }
    }

    private void Settins_Click(object sender, RoutedEventArgs e)
    {
      if (SettingsWindow.ShowSettingsDialog() == true)
      {
        // TODO сделать применение настроек без перезапуска приложения.
        MessageBox.Show("Settings will be applied after restarting the application");
        Application.Current.Shutdown();
      }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
      Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
      e.Cancel = true;
      this.Hide();
      base.OnClosing(e);
    }

    private void TaskbarIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e)
    {
      BringToForeground();
    }

    public void BringToForeground()
    {
      if (this.WindowState == WindowState.Minimized || this.Visibility == Visibility.Hidden)
      {
        this.Show();
        this.WindowState = WindowState.Maximized;
      }

      this.Activate();
      this.Topmost = true;
      this.Topmost = false;
      this.Focus();
    }

    private async void Filter_TextChanged(object sender, TextChangedEventArgs e)
    {
      TextBox tb = (TextBox)sender;
      int startLength = tb.Text.Length;

      await Task.Delay(500);

      if (startLength == tb.Text.Length && tb.IsEnabled)
        FilterLines(tb.Text);
    }

    private void FilterLines(string text)
    {
      if (!String.IsNullOrEmpty(text))
      {
        var upperFilterText = text.ToUpper();
        LogsGrid.ItemsSource = logLines.Where(l => l.Level.ToUpper().Contains(upperFilterText) || l.FullMessage.ToUpper().Contains(upperFilterText));
      }
      else
      {
        LogsGrid.ItemsSource = logLines;

        if (LogsGrid.SelectedItem != null)
          LogsGrid.ScrollIntoView(LogsGrid.SelectedItem);
      }
    }

    private void CopyCommand(object sender, ExecutedRoutedEventArgs e)
    {
      var sb = new StringBuilder();
      foreach (var item in LogsGrid.SelectedItems)
      {
        var logLine = (LogLine)item;
        var logLineElements = Converter.ConvertObjectToDict(logLine);
        sb.AppendLine(Converter.TsvFormat(logLineElements));
      }
      Clipboard.SetText(sb.ToString());
    }
  }
}
