using System;
using System.Windows;
using System.Windows.Controls;

namespace ParsecVDisplay.Components
{
    public partial class Button : UserControl
    {
        public event EventHandler Click;
        public new object Content { get; set; }

        public static readonly new DependencyProperty ContentProperty =
            DependencyProperty.Register("Content", typeof(object), typeof(Button), new PropertyMetadata(null));

        public Button()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(sender, e);
        }
    }
}