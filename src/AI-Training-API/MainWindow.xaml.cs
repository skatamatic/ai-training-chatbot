using System.Collections.Specialized;
using System.Windows;

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
    }
}
