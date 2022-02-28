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
using System.Windows.Data;
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

    private readonly string All = "All";

    private readonly List<LogHandler> LogHandlers = new List<LogHandler>();

    private readonly ObservableCollection<LogLine> logLines = new ObservableCollection<LogLine>();

    private readonly Uri notificationIcon;

    private readonly string iconFileName = "horse.png";

    private readonly int gridUpdatePeriod = 1000;

    private ICollectionView logLinesView;

    private LogWatcher logWatcher;

    private ScrollViewer gridScrollViewer;

    private string openedFileFullPath;

    private bool filterChanged = false;

    private List<string> HiddenColumns = new List<string> { "Pid", "Trace", "Tenant" };
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
      
      InitTenantFilter();
      InitLevelFilter();

      logLinesView = CollectionViewSource.GetDefaultView(logLines);
      logLinesView.Filter = null;
    }

    private void InitTenantFilter()
    {
      TenantFilter.Items.Clear();
      TenantFilter.Items.Add(All);
      TenantFilter.SelectedValue = All;
    }
    private void InitLevelFilter()
    {
      LevelFilter.Items.Clear();
      LevelFilter.Items.Add(All);
      LevelFilter.SelectedValue = All;
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
                filterChanged = true;
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
      SearchGrid.ItemsSource = null;
      logLines.Clear();
      InitTenantFilter();
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
        gridScrollViewer.ScrollToEnd();
        logWatcher.StartWatch(gridUpdatePeriod);

        var tenants = logLines.Where(l => !string.IsNullOrEmpty(l.Tenant)).Select(l => l.Tenant).Distinct();

        foreach (var tenant in tenants)
        {
          TenantFilter.Items.Add(tenant);
        }

        var levels = logLines.Where(l => !string.IsNullOrEmpty(l.Level)).Select(l => l.Level).Distinct();

        foreach (var level in levels)
        {
          LevelFilter.Items.Add(level);
        }
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
        GC.Collect();
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

      openedFileFullPath = selectedItem.FullPath;
      OpenLogFile(openedFileFullPath);
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

          if (scrollToEnd)
            LogsGrid.ScrollIntoView(logLine);
        }));
    }

    private void OnFileReCreated()
    {
      Application.Current.Dispatcher.Invoke(new Action(() =>
      {
        CloseLogFile();
        if (!string.IsNullOrEmpty(openedFileFullPath))
          OpenLogFile(openedFileFullPath);
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

        if (!String.IsNullOrEmpty(line.Pid))
          DetailText.Text += $"Pid: {line.Pid} \n";

        if (!String.IsNullOrEmpty(line.Trace))
          DetailText.Text += $"Trace: {line.Trace} \n";

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
      filterChanged = true;

      await Task.Delay(900);

      if (startLength == tb.Text.Length && tb.IsEnabled)
        FilterLines(tb.Text);
    }

    private bool GetFilterLine(LogLine line, string text)
    {
      var result = true;

      if (!string.IsNullOrEmpty(text))
      {

        var upperText = text.ToUpper();
        result = (line.FullMessage != null && line.FullMessage.ToUpper().Contains(upperText))
          || (line.Trace != null && line.Trace.ToUpper().Contains(upperText))
          || (line.Pid != null && line.Pid.ToUpper().Contains(upperText));
      }

      var tenantFilter = TenantFilter.SelectedValue as string;

      if (!string.IsNullOrEmpty(tenantFilter) && !string.Equals(tenantFilter, All))
      {
        result = result && string.Equals(line.Tenant, tenantFilter);
      }

      var levelFilter = LevelFilter.SelectedValue as string;

      if (!string.IsNullOrEmpty(levelFilter) && !string.Equals(levelFilter, All))
      {
        result = result && line.Level != null && string.Equals(line.Level, levelFilter);
      }

      return result;
    }

    private bool LogLinesFilter(object item)
    {
      LogLine line = item as LogLine;
      return GetFilterLine(line, Filter.Text);
    }

    private void FilterLines(string text)
    {
      if (logLinesView == null)
        return;

      if(filterChanged)
      {
        logLinesView.Filter = null;

        if (LogsGrid.SelectedItem != null)
          LogsGrid.ScrollIntoView(LogsGrid.SelectedItem);
      }

      var tenantFilter = TenantFilter.SelectedValue as string;
      var levelFilter = LevelFilter.SelectedValue as string;

      if (!String.IsNullOrEmpty(text) || (!String.Equals(tenantFilter, All) && !String.IsNullOrEmpty(tenantFilter))
        || (!String.Equals(levelFilter, All) && !String.IsNullOrEmpty(levelFilter)))
      {
        if (logLinesView.Filter == null)
          logLinesView.Filter = LogLinesFilter;
        else
          logLinesView.Refresh();
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

    private void SearchText(Object sender, ExecutedRoutedEventArgs e)
    {
      if (!LogsFileNames.IsEnabled)
        return;

      var dialog = new SearchWindow();
      var result = dialog.ShowDialog();

      if (result == true)
      {
        SearchGrid.ItemsSource = logLines.Where(l => GetFilterLine(l, dialog.SearchText.Text)).ToList();
        BottomTabControl.SelectedItem = SearchTab;
      }
    }

    private void SearchGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      LogLine line = (sender as DataGrid).SelectedItem as LogLine;

      if (line != null)
      {
        LogsGrid.SelectedItem = line;
        LogsGrid.ScrollIntoView(line);
      }
    }
    private void FilterTenant_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var tenant = (sender as ComboBox).SelectedItem as string;

      if (tenant != null)
      {
        filterChanged = true;
        FilterLines(Filter.Text);
      }
    }

    private void FilterLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var level = (sender as ComboBox).SelectedItem as string;

      if (level != null)
      {
        filterChanged = true;
        FilterLines(Filter.Text);
      }
    }
    private void ColumnVisibilityCheck(object sender, RoutedEventArgs e)
    {
      foreach (var hiddenColumns in LogsGrid.Columns.Where(c => HiddenColumns.Contains(c.Header)))
      {
        hiddenColumns.Visibility = Visibility.Visible;
      }
    }

    private void ColumnVisibilityUnchecked(object sender, RoutedEventArgs e)
    {
      foreach (var hiddenColumns in LogsGrid.Columns.Where(c => HiddenColumns.Contains(c.Header)))
      {
        hiddenColumns.Visibility = Visibility.Collapsed;
      }
    }
  }
}
