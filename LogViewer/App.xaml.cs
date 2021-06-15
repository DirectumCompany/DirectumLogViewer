using System;
using System.Threading;
using System.Windows;

namespace LogViewer
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App : Application
  {
    private const string UniqueEventName = "fec06a50-4749-41a2-9eec-5374ad5ecdb1";
    private const string UniqueMutexName = "aebb638c-600a-4cc7-bafd-0a13aee71edd";
    private EventWaitHandle eventWaitHandle;
    private Mutex mutex;

    private void AppOnStartup(object sender, StartupEventArgs e)
    {
      mutex = new Mutex(true, UniqueMutexName, out bool isOwned);
      eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, UniqueEventName);

      GC.KeepAlive(this.mutex);

      if (isOwned)
      {
        var thread = new Thread(() =>
            {
              while (this.eventWaitHandle.WaitOne())
              {
                Current.Dispatcher.BeginInvoke((Action)(() => ((MainWindow)Current.MainWindow).BringToForeground()));
              }
            });

        thread.IsBackground = true;

        thread.Start();
        return;
      }

      this.eventWaitHandle.Set();

      this.Shutdown();
    }

  }
}
