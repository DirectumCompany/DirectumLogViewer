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
using System.Text.RegularExpressions;
using SshConfigParser;
using Renci.SshNet;
using Microsoft.Win32;
using System.Globalization;

namespace LogViewer
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    public const string WindowTitle = "Directum Log Viewer";

    public const string NotificationError = "NotificationError";

    public const string NotificationTypeKey = "Type";

    public const string NotificationFilePathKey = "FilePath";

    public const string NotificationTimeKey = "Time";

    private const string OpenAction = "OpenAction";

    private const string AddRemoteHostAction = "AddRemoteHost";

    private const string All = "All";

    private const string IconFileName = "horse.png";
    private const int GridUpdatePeriod = 1000;

    // UseRegex is binding proprety
    public bool UseRegex { get; set; }

    private readonly List<LogHandler> logHandlers = new List<LogHandler>();

    private readonly ObservableCollection<LogLine> logLines = new ObservableCollection<LogLine>();

    private static string SshConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "config");

    private ObservableCollection<LogLine> filteredLogLines;

    private readonly Uri notifyLogo;

    private ICollectionView logLinesView;

    private LogWatcher logWatcher;

  //  private RemoteLogWatcher remoteLogWatcher;

    private ScrollViewer gridScrollViewer;

    private string openedFileFullPath;

    private readonly string[] hiddenColumns = { "Pid", "Trace", "Tenant" };
    private IEnumerable<SshHost> KnownHosts { get; set; }

    private const string RegKey = @"SOFTWARE\JsonLogViewerSettings\RemoteHost\";

    private ConnectionInfo connectionInfo;


    public MainWindow()
    {
      InitializeComponent();

      DataContext = this;

      SettingsWindow.Load();

      if (SettingsWindow.IsFirstRun() && !ShowSettingsWindow())
      {
        Application.Current.Shutdown();
        return;
      }

      if (!Directory.Exists(SettingsWindow.LogsPath) && !ShowSettingsWindow())
      {
        Application.Current.Shutdown();
        return;
      }

      notifyLogo = GetNotifyLogo();

      var files = FindLogs(SettingsWindow.LogsPath);

      if (files != null && SettingsWindow.UseBackgroundNotification)
        CreateHandlers(files);

      InitControls(files);

      if (SettingsWindow.UseBackgroundNotification)
        SetNotificationActivated();
    }

    private void Window_ContentRendered(object sender, EventArgs e)
    {
      this.Title = WindowTitle;
      gridScrollViewer = GetScrollViewer(LogsGrid);

      var args = Environment.GetCommandLineArgs();
      if (args.Length > 1)
      {
        var fileName = args[1];

        if (File.Exists(fileName) && Path.GetExtension(fileName) == ".log")
          SelectFileToOpen(fileName);
      }
    }

    private bool ShowSettingsWindow()
    {
      var result = SettingsWindow.ShowSettingsDialog() == true;

      if (result)
        ApplySettings();

      return result;
    }

    private Uri GetNotifyLogo()
    {
      string directory = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
      string imageFilePath = Path.Combine(directory, IconFileName);

      if (!File.Exists(imageFilePath))
      {
        ImageConverter converter = new ImageConverter();
        byte[] data = (byte[])converter.ConvertTo(Properties.Resources.horse, typeof(byte[]));
        File.WriteAllBytes(imageFilePath, data);
      }

      return new Uri(imageFilePath);
    }

    private string[] FindLogs(string directory)
    {
      if (!Directory.Exists(directory))
        return null;

      string[] allfiles = Directory.GetFiles(directory, "*.log", SearchOption.AllDirectories);

      string machineName = System.Environment.MachineName.ToLower();
      var currentDate = DateTime.Today.ToString("yyyy-MM-dd");

      var whiteList = SettingsWindow.WhitelistLogs.Split(new[] { '\r', '\n' })
        .Select(s => s.Trim().ToLower().Replace("${machinename}", machineName).Replace("${shortdate}", currentDate))
        .Where(s => !String.IsNullOrEmpty(s))
        .ToArray();

      return allfiles.Select(f => new LogFile(f))
        .Where(n => whiteList.Contains(System.IO.Path.GetFileNameWithoutExtension(n.Name.ToLower())))
        .Select(r => r.FullPath)
        .ToArray();
    }

    private void CreateHandlers(string[] files)
    {
      foreach (var file in files)
        Task.Run(() => logHandlers.Add(new LogHandler(file, this.notifyLogo)));
    }

    private void InitControls(string[] files)
    {
      InitLogFiles(files);
      InitTenantFilter();
      InitLevelFilter();
      InitLoggerFilter();
      InitHosts();
      logLinesView = CollectionViewSource.GetDefaultView(logLines);
    }

    private void InitLogFiles(string[] files)
    {
      LogsFileNames.Items.Clear();
      foreach (var file in files)
        LogsFileNames.Items.Add(new LogFile(file));

      LogsFileNames.Items.Add(new LogFile(OpenAction, "Open from file..."));
    }
    private void InitTenantFilter()
    {
      TenantFilter.Items.Clear();
      TenantFilter.Items.Add(All);
      TenantFilter.SelectedValue = All;
    }
    private void InitLoggerFilter()
    {
      LoggerFilter.Items.Clear();
      LoggerFilter.Items.Add(All);
      LoggerFilter.SelectedValue = All;
    }

    private void InitLevelFilter()
    {
      LevelFilter.Items.Clear();
      LevelFilter.Items.Add(All);
      LevelFilter.Items.Add("Trace");
      LevelFilter.Items.Add("Debug");
      LevelFilter.Items.Add("Info");
      LevelFilter.Items.Add("Warn");
      LevelFilter.Items.Add("Error");
      LevelFilter.Items.Add("Fatal");

      LevelFilter.SelectedValue = All;
    }

    private void InitHosts()
    {
      HostFilter.Items.Clear();
      HostFilter.Items.Add(new SshHost { Name = Environment.MachineName, LogsFolder = SettingsWindow.LogsPath, IsRemote = false });
      KnownHosts = GetHostsFromRegistry();

     // var config = SshConfig.ParseFile(SshConfigPath);
      foreach (var host in KnownHosts)
        HostFilter.Items.Add(host);

      HostFilter.Items.Add(new SshHost { Name = "Add remote host...", LogsFolder = AddRemoteHostAction, IsRemote = false });
      HostFilter.SelectedIndex = 0;
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
                Filter.Text = null;

              LevelFilter.SelectedValue = All;

              SetFilter(string.Empty, All, All, All);
              LogsGrid.SelectedItem = itemWithError;
              LogsGrid.ScrollIntoView(itemWithError);
              Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => LogsGrid.Focus()));
            }
          });
        }
      };
    }

    /// <summary>
    /// Закрытие лог файла с уничтожением потоков.
    /// </summary>
    private void CloseLogFile(LogWatcher logWatcher)
    {
      // Clear previous log resources
      if (logWatcher != null)
      {
        logWatcher.Dispose();
        logWatcher = null;
      }

      logLines.Clear();
      InitTenantFilter();
      InitLevelFilter();
      InitLoggerFilter();
      this.Title = WindowTitle;
      LogsGrid.ItemsSource = null;
      SearchGrid.ItemsSource = null;
      filteredLogLines = null;
      GC.Collect();
    }

    /// <summary>
    /// Открытие файла с прочтением данных и настройкой дальнейшего слежения за ним.
    /// </summary>
    /// <param name="fullPath">Путь до файла.</param>
    private void OpenLogFile(string fullPath)
    {
      try
      {
        LoadBar.Visibility = Visibility.Visible;
        LogsFileNames.IsEnabled = false;
        Filter.IsEnabled = false;
        LogsGrid.IsEnabled = false;
        ColumnVisibilityToggleBtn.IsEnabled = false;
        TenantFilter.IsEnabled = false;
        LevelFilter.IsEnabled = false;
        LoggerFilter.IsEnabled = false;
        Filter.Clear();

        this.Title = string.Format($"{WindowTitle} ({fullPath})");
        LogsGrid.ItemsSource = null;
        filteredLogLines = null;

        if (connectionInfo != null)
          logWatcher = new RemoteLogWatcher(fullPath, connectionInfo);
        else 
          logWatcher = new LogWatcher(fullPath);
        
        logWatcher.BlockNewLines += OnBlockNewLines;
        logWatcher.FileReCreated += OnFileReCreated;
        logWatcher.ReadToEndLine();
        LogsGrid.ItemsSource = logLines;
        if (logLines.Any())
          LogsGrid.ScrollIntoView(logLines.Last());

        logWatcher.StartWatch(GridUpdatePeriod);

        var tenants = logLines.Where(l => !string.IsNullOrEmpty(l.Tenant)).Select(l => l.Tenant).Distinct().OrderBy(l => l);

        foreach (var tenant in tenants)
        {
          TenantFilter.Items.Add(tenant);
        }

        var loggers = logLines.Where(l => !string.IsNullOrEmpty(l.Logger)).Select(l => l.Logger).Distinct().OrderBy(l => l);

        foreach (var logger in loggers)
        {
          LoggerFilter.Items.Add(logger);
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
        ColumnVisibilityToggleBtn.IsEnabled = true;
        TenantFilter.IsEnabled = true;
        LevelFilter.IsEnabled = true;
        LoggerFilter.IsEnabled = true;
        GC.Collect();
      }
    }

    /// <summary>
    /// Метод подмены файла
    /// </summary>
    private void Files_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var filterValue = Filter.Text;
      var levelValue = LevelFilter.SelectedValue;

      CloseLogFile(logWatcher);

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
          SelectFileToOpen(dialog.FileName);
        else
          comboBox.SelectedItem = null;

        return;
      }

      comboBox.Items.Refresh();

      openedFileFullPath = selectedItem.FullPath;
      OpenLogFile(openedFileFullPath);

      Filter.Text = filterValue;
      LevelFilter.SelectedValue = levelValue;
    }
    /// <summary>
    /// Обработка блока прочитанных строк.
    /// </summary>
    /// <param name="lines">Новые строки.</param>
    /// <param name="progress"></param>
    private void OnBlockNewLines(List<string> lines, double progress)
    {
      var convertedLogLines = Converter.ConvertLinesToObjects(lines);

      Application.Current.Dispatcher.Invoke(
        new Action(() =>
        {
          if (LoadBar.Visibility == Visibility.Visible && LoadBar.Value != progress)
            LoadBar.Dispatcher.Invoke(() => LoadBar.Value = progress, DispatcherPriority.Background);

          var scrollToEnd = false;

          if (gridScrollViewer != null)
          {
            gridScrollViewer.UpdateLayout();

            if (gridScrollViewer.VerticalOffset == gridScrollViewer.ScrollableHeight)
              scrollToEnd = true;
          }

          foreach (var logLine in convertedLogLines)
          {
            logLines.Add(logLine);

            if (filteredLogLines != null)
            {
              var tenant = TenantFilter.SelectedValue as string;
              var level = LevelFilter.SelectedValue as string;
              var logger = LoggerFilter.SelectedValue as string;

              if (NeedShowLine(logLine, Filter.Text, tenant, level, logger, this.UseRegex))
                filteredLogLines.Add(logLine);
            }
          }

          if (scrollToEnd && convertedLogLines.Any())
            LogsGrid.ScrollIntoView(convertedLogLines.Last());

        }));
    }

    /// <summary>
    /// Переоткрытие файла.
    /// </summary>
    private void OnFileReCreated()
    {
      Application.Current.Dispatcher.Invoke(new Action(() =>
      {
        if (logLines != null)
          logLines.Clear();

        if (filteredLogLines != null)
          filteredLogLines.Clear();
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
          DetailText.Text += $"UserName: {line.UserName}\n";

        if (!String.IsNullOrEmpty(line.Tenant))
          DetailText.Text += $"Tenant: {line.Tenant}\n";

        if (!String.IsNullOrEmpty(line.Pid))
          DetailText.Text += $"Pid: {line.Pid}\n";

        if (!String.IsNullOrEmpty(line.Trace))
          DetailText.Text += $"Trace: {line.Trace}\n";

        if (!String.IsNullOrEmpty(line.Version))
          DetailText.Text += $"Version: {line.Version}\n";

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
      if (ShowSettingsWindow())
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

      int delay = UseRegex ? 5000 : 1500;

      await Task.Delay(delay);

      if (startLength == tb.Text.Length && tb.IsEnabled && e.UndoAction != UndoAction.Clear)
      {
        var tenant = TenantFilter.SelectedValue as string;
        var level = LevelFilter.SelectedValue as string;
        var logger = LoggerFilter.SelectedValue as string;
        SetFilter(tb.Text, tenant, level, logger);
      }
    }

    private bool NeedShowLine(LogLine line, string filter, string tenant, string level, string logger, bool useRegex)
    {
      var result = true;

      if (result && !string.IsNullOrEmpty(filter))
      {
        if (useRegex)
        {
          try
          {
            Regex regex = new Regex(filter, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            result = !string.IsNullOrEmpty(line.FullMessage) && regex.IsMatch(line.FullMessage);
          }
          catch (RegexParseException)
          {
            result = false;
          }
        }
        else
          result = !string.IsNullOrEmpty(line.FullMessage) && line.FullMessage.IndexOf(filter, StringComparison.OrdinalIgnoreCase) > -1;

        result = result || (!string.IsNullOrEmpty(line.Trace) && line.Trace.IndexOf(filter, StringComparison.OrdinalIgnoreCase) > -1) ||
                           (!string.IsNullOrEmpty(line.Pid) && line.Pid.IndexOf(filter, StringComparison.OrdinalIgnoreCase) > -1) ||
                           (!string.IsNullOrEmpty(line.Level) && line.Level.IndexOf(filter, StringComparison.OrdinalIgnoreCase) > -1);
      }



      if (result && !string.IsNullOrEmpty(tenant) && !string.Equals(tenant, All, StringComparison.InvariantCultureIgnoreCase))
      {
        result = result && string.Equals(line.Tenant, tenant, StringComparison.InvariantCultureIgnoreCase);
      }

      if (result && !string.IsNullOrEmpty(level) && !string.Equals(level, All, StringComparison.InvariantCultureIgnoreCase))
      {
        result = result && line.Level != null && string.Equals(line.Level, level, StringComparison.InvariantCultureIgnoreCase);
      }

      if (result && !string.IsNullOrEmpty(logger) && !string.Equals(logger, All, StringComparison.InvariantCultureIgnoreCase))
      {
        result = result && line.Logger != null && string.Equals(line.Logger, logger, StringComparison.InvariantCultureIgnoreCase);
      }

      return result;
    }

    private void SetFilter(string includeFilter, string tenant, string level, string logger)
    {
      if (logLinesView == null)
        return;

      var needFilter = !String.IsNullOrEmpty(includeFilter) ||
                       (!String.Equals(tenant, All) && !String.IsNullOrEmpty(tenant)) ||
                       (!String.Equals(level, All) && !String.IsNullOrEmpty(level)) ||
                       (!String.Equals(logger, All) && !String.IsNullOrEmpty(logger));

      if (needFilter)
      {
        using (new WaitCursor())
        {
          filteredLogLines = new ObservableCollection<LogLine>(logLines.Where(l => NeedShowLine(l, includeFilter, tenant, level, logger, this.UseRegex)));
          LogsGrid.ItemsSource = filteredLogLines;
        }
      }
      else
      {
        filteredLogLines = null;
        LogsGrid.ItemsSource = logLines;
      }

      if (LogsGrid.SelectedItem != null)
        LogsGrid.ScrollIntoView(LogsGrid.SelectedItem);

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
      dialog.Owner = this;
      var result = dialog.ShowDialog();

      if (result == true)
      {
        var tenant = TenantFilter.SelectedValue as string;
        var level = LevelFilter.SelectedValue as string;
        var logger = LoggerFilter.SelectedValue as string;

        using (new WaitCursor())
        {
          SearchGrid.ItemsSource = logLines.Where(l => NeedShowLine(l, dialog.SearchText.Text, tenant, level, logger, dialog.UseRegex.IsChecked.Value)).ToList();
          BottomTabControl.SelectedItem = SearchTab;
        }
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
        var level = LevelFilter.SelectedValue as string;
        var logger = LoggerFilter.SelectedValue as string;
        SetFilter(Filter.Text, tenant, level, logger);
      }
    }

    private void FilterLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var level = (sender as ComboBox).SelectedItem as string;

      if (level != null)
      {
        var tenant = TenantFilter.SelectedValue as string;
        var logger = LoggerFilter.SelectedValue as string;
        SetFilter(Filter.Text, tenant, level, logger);
      }
    }
    private void FilterLogger_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var logger = (sender as ComboBox).SelectedItem as string;

      if (logger != null)
      {
        var tenant = TenantFilter.SelectedValue as string;
        var level = LevelFilter.SelectedValue as string;
        SetFilter(Filter.Text, tenant, level, logger);
      }
    }
    private void ColumnVisibilityCheck(object sender, RoutedEventArgs e)
    {
      foreach (var column in LogsGrid.Columns.Where(c => hiddenColumns.Contains(c.Header)))
      {
        column.Visibility = Visibility.Visible;
      }
    }

    private void ColumnVisibilityUnchecked(object sender, RoutedEventArgs e)
    {
      foreach (var columns in LogsGrid.Columns.Where(c => hiddenColumns.Contains(c.Header)))
      {
        columns.Visibility = Visibility.Collapsed;
      }
    }

    private void ApplySettings()
    {
      if (SettingsWindow.AssociateLogFileChanged)
      {
        if (SettingsWindow.AssociateLogFile == true)
          FileAssociations.SetAssociation();
        else
          FileAssociations.RemoveAssociation();
      }
    }

    private void SelectFileToOpen(string fileName)
    {
      var logFiles = LogsFileNames.Items.Cast<LogFile>().ToList();

      var logFile = logFiles.FirstOrDefault(l => string.Equals(l.FullPath, fileName, StringComparison.InvariantCultureIgnoreCase));

      if (logFile != null)
      {
        LogsFileNames.SelectedItem = logFile;
      }
      else
      {
        // Создать фоновый обработчик для нового файла.
        if (SettingsWindow.UseBackgroundNotification)
          logHandlers.Add(new LogHandler(fileName, notifyLogo));

        logFile = new LogFile(fileName);
        LogsFileNames.Items.Insert(LogsFileNames.Items.Count - 1, logFile);
        LogsFileNames.SelectedItem = logFile;
      }
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

    #region Убираем авто-скрол при клике по колонке Message или нажатии навигационных кнопок(up/down/pageup/pagedown) на клавиатуре.

    private void LogsGrid_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
      e.Handled = !(e.Source is System.Windows.Controls.DataGridRow);
    }

    private void LogsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
      if (gridScrollViewer != null)
        // Делаем свои обработчики на кнопки.
        NavigationKeyDown(gridScrollViewer, e);
    }

    private void NavigationKeyDown(ScrollViewer scrollViewer, KeyEventArgs e)
    {
      bool controlDown = ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0);
      bool altDown = ((e.KeyboardDevice.Modifiers & ModifierKeys.Alt) != 0);

      if (!altDown)
      {
        bool invertForRTL = (FlowDirection == FlowDirection.RightToLeft);
        switch (e.Key)
        {
          case Key.Left:
            if (invertForRTL) scrollViewer.LineRight(); else scrollViewer.LineLeft();
            e.Handled = true;
            break;

          case Key.Right:
            if (invertForRTL) scrollViewer.LineLeft(); else scrollViewer.LineRight();
            e.Handled = true;
            break;

          case Key.Up:
            if (LogsGrid.SelectedIndex != -1)
            {
              if (LogsGrid.SelectedIndex > 0)
                LogsGrid.SelectedItem = LogsGrid.Items[LogsGrid.SelectedIndex - 1];

              LogsGrid.ScrollIntoView(LogsGrid.SelectedItem);

              DataGridRow row = (DataGridRow)LogsGrid.ItemContainerGenerator.ContainerFromIndex(LogsGrid.SelectedIndex);
              row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Up));
            }
            e.Handled = true;
            break;

          case Key.Down:
            if (LogsGrid.SelectedIndex != -1)
            {
              if (LogsGrid.SelectedIndex + 1 < LogsGrid.Items.Count)
                LogsGrid.SelectedItem = LogsGrid.Items[LogsGrid.SelectedIndex + 1];

              LogsGrid.ScrollIntoView(LogsGrid.SelectedItem);

              DataGridRow row = (DataGridRow)LogsGrid.ItemContainerGenerator.ContainerFromIndex(LogsGrid.SelectedIndex);
              row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
            }
            e.Handled = true;
            break;

          case Key.PageUp:
          case Key.PageDown:
            OnPageUpOrDownKeyDown(scrollViewer, e);
            break;

          case Key.Home:
            if (controlDown)
            {
              scrollViewer.ScrollToTop();

              if (LogsGrid.Items.Count > 0)
                LogsGrid.SelectedItem = LogsGrid.Items[0];
            }
            else scrollViewer.ScrollToLeftEnd();
            e.Handled = true;
            break;

          case Key.End:
            if (controlDown)
            {
              scrollViewer.ScrollToBottom();

              if (LogsGrid.Items.Count > 0)
                LogsGrid.SelectedItem = LogsGrid.Items[LogsGrid.Items.Count - 1];
            }
            else scrollViewer.ScrollToRightEnd();
            e.Handled = true;
            break;
        }
      }
    }

    private void OnPageUpOrDownKeyDown(ScrollViewer scrollHost, KeyEventArgs e)
    {
      if (scrollHost != null)
      {
        e.Handled = true;

        int rowIndex = LogsGrid.SelectedIndex;
        if (rowIndex >= 0)
        {
          int jumpDistance = Math.Max(1, (int)scrollHost.ViewportHeight - 1);
          int targetIndex = (e.Key == Key.PageUp) ? rowIndex - jumpDistance : rowIndex + jumpDistance;
          targetIndex = Math.Max(0, Math.Min(targetIndex, LogsGrid.Items.Count - 1));

          LogsGrid.SelectedItem = LogsGrid.Items[targetIndex];
          LogsGrid.ScrollIntoView(LogsGrid.SelectedItem);

          FocusNavigationDirection direction = e.Key == Key.PageUp ? FocusNavigationDirection.Up : FocusNavigationDirection.Down;
          DataGridRow row = (DataGridRow)LogsGrid.ItemContainerGenerator.ContainerFromIndex(LogsGrid.SelectedIndex);
          row.MoveFocus(new TraversalRequest(direction));
        }
      }
    }
    #endregion

    private void RegexButton_Click(object sender, RoutedEventArgs e)
    {
      ContextMenu cm = this.FindResource("RegexButton") as ContextMenu;
      cm.PlacementTarget = sender as Button;
      cm.IsOpen = true;
    }

    private void RegexExample1_Click(object sender, RoutedEventArgs e)
    {
      this.Filter.Text = "cat|dog";
      UseRegex = true;
    }

    private void RegexExample2_Click(object sender, RoutedEventArgs e)
    {
      this.Filter.Text = "(?=.*cat)(?=.*dog)";
      UseRegex = true;
    }

    private void RegexExample3_Click(object sender, RoutedEventArgs e)
    {
      this.Filter.Text = "(?=.*cat)(?=.*dog)(^(?!.*(horse|pig)).*$)";
      UseRegex = true;
    }

    private void UseRegex_Click(object sender, RoutedEventArgs e)
    {
      if (this.Filter.IsEnabled)
      {
        var tenant = this.TenantFilter.SelectedValue as string;
        var level = this.LevelFilter.SelectedValue as string;
        var logger = this.LoggerFilter.SelectedValue as string;
        this.SetFilter(this.Filter.Text, tenant, level, logger);
      }
    }

    private void Host_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      var comboBox = sender as ComboBox;
      var selectedItem = comboBox.SelectedItem as SshHost;

      if (selectedItem != null)
      {
        if (selectedItem.LogsFolder == AddRemoteHostAction)
        {
          var window = new RemoteHostWindow();
          if (window.ShowDialog() ?? false)
          {
            var host = new SshHost 
            { 
              Name = window.Name.Text, 
              IsRemote = true, 
              Host = window.Host.Text,
              User = window.Username.Text,
              LogsFolder = window.LogsPath.Text,
              Password = window.Password.Text,
              IdentityFile = window.IdentityFile.Text
            };
            HostFilter.Items.Insert(HostFilter.Items.Count - 1, host);
            HostFilter.SelectedItem = host;
            return;
          }
          else
          {
            HostFilter.SelectedItem = null;
            return;
          }
        }
        string[] files;
        if (selectedItem.IsRemote)
        {
          connectionInfo = new ConnectionInfo(selectedItem.Host, selectedItem.User, new PasswordAuthenticationMethod(selectedItem.User, selectedItem.Password));
          using (var client = new SftpClient(connectionInfo))
          {
            client.Connect();
            files = client.ListDirectory(selectedItem.LogsFolder)?.Where(x => x.Name.Contains(".log"))?.Select(x => x.FullName).ToArray();
            client.Disconnect();
          }
        }
        else
        {
          connectionInfo = null;
          files = FindLogs(SettingsWindow.LogsPath);
        }

        InitLogFiles(files);
      }
    }

    private IEnumerable<SshHost> GetHostsFromRegistry()
    {
      var hostsProperties = Registry.CurrentUser.OpenSubKey(RegKey).GetSubKeyNames();
      var result = new List<SshHost>();
      foreach (var hostProperties in hostsProperties)   
        result.Add(ParseHost(RegKey + hostProperties));

      return result;
    }

    private SshHost ParseHost(string regKey)
    {
      var host = new SshHost();
      var key = Registry.CurrentUser.OpenSubKey(regKey);
      var properties = key.GetValueNames();
      foreach (var property in properties)
      {
        switch(property)
        {
          case "Host": host.Host = key.GetValue(property)?.ToString(); break;
          case "Name": host.Name = key.GetValue(property)?.ToString(); break;
          case "LogsFolder": host.LogsFolder = key.GetValue(property)?.ToString(); ; break;
          case "User": host.User = key.GetValue(property)?.ToString(); ; break;
          case "Password": host.Password = key.GetValue(property)?.ToString(); ; break;
          case "IdentityFile": host.IdentityFile = key.GetValue(property)?.ToString(); ; break;
          default: break;
        }
      }

      return host;
    }
  }
}
