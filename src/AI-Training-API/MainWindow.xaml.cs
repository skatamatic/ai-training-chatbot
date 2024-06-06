using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AI_Training_API
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.Messages.CollectionChanged += (s, e) =>
                {
                    if (e.Action == NotifyCollectionChangedAction.Add)
                    {
                        scrollViewer.ScrollToEnd();
                    }

                    vm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(MainWindowViewModel.IsReady) && vm.IsReady)
                        {
                            inputField.Focus();
                        }
                    };
                };
            }

            inputField.Focus();
        }

        private void MarkdownScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true; // Mark the event as handled
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = MouseWheelEvent,
                    Source = sender
                };

                scrollViewer.RaiseEvent(eventArg);
            }
        }

        
    }
}
