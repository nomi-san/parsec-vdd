using System;
using System.Windows;
using System.Windows.Controls;

namespace ParsecVDisplay.Components
{
    public partial class Button : UserControl
    {
        public event EventHandler Click;
        public object Children { get; set; }

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