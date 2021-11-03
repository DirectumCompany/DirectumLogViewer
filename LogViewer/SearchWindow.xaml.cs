using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LogViewer
{
  /// <summary>
  /// Interaction logic for SearchWindow.xaml
  /// </summary>
  public partial class SearchWindow : Window
  {
    public SearchWindow()
    {
      InitializeComponent();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
      this.DialogResult = true;
    }

    private void SearchText_KeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Return)
      {
        this.Accept_Click(null, null);
      }
    }
  }
}
