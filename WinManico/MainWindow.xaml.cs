using System.Windows;
using System.ComponentModel;
using WinManico.ViewModels;

using WinManico.Core;

namespace WinManico
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;
            
            // Listen to property changes
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            // Start hidden
            this.Visibility = Visibility.Collapsed;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure window starts hidden
            this.Visibility = Visibility.Collapsed;
            System.Console.WriteLine("[WINDOW] Window loaded, setting initial visibility to Collapsed");

            // Hide from Alt-Tab by setting WS_EX_TOOLWINDOW
            var wih = new System.Windows.Interop.WindowInteropHelper(this);
            int exStyle = NativeMethods.GetWindowLong(wih.Handle, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(wih.Handle, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsVisible))
            {
                // Directly control window visibility
                var shouldBeVisible = _viewModel.IsVisible;
                var newVisibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
                
                // Only change if actually different to prevent feedback loops
                if (this.Visibility != newVisibility)
                {
                    System.Console.WriteLine($"[WINDOW] Property changed - IsVisible={shouldBeVisible}, Setting Window.Visibility={newVisibility}");
                    this.Visibility = newVisibility;
                }
            }
        }
    }
}