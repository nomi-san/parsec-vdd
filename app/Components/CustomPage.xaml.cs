using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ParsecVDisplay.Components
{
    public partial class CustomPage : Page
    {
        TextBox[] TextBoxes;

        public CustomPage()
        {
            InitializeComponent();

            xParentGPU.Items.Clear();
            foreach (var item in Enum.GetValues(typeof(ParsecVDD.ParentGPU)))
            {
                xParentGPU.Items.Add(item.ToString());
            }
        }

        private void ApplyChanges(object sender, EventArgs e)
        {
            var modes = new List<Display.Mode>();

            for (int i = 0; i < 15; i += 3)
            {
                var tw = TextBoxes[i].Text;
                var th = TextBoxes[i + 1].Text;
                var thz = TextBoxes[i + 2].Text;

                if (int.TryParse(tw, out var width)
                    && int.TryParse(th, out var height)
                    && int.TryParse(thz, out var hz))
                {
                    // Check negative values & limit 8K resolution
                    if (width < 0 || width > 7680 || height < 0 || height > 4320 || hz < 0)
                    {
                        MessageBox.Show(App.GetTranslation("t_msg_custom_invalid_slot", i / 3 + 1),
                            App.NAME, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    else
                    {
                        modes.Add(new Display.Mode(width, height, hz));
                    }
                }
            }

            ParsecVDD.ParentGPU parentGPU;
            bool validParentGPU = Enum.TryParse(xParentGPU.SelectedValue.ToString(), true, out parentGPU);

            if (modes.Count > 0 && validParentGPU)
            {
                if (Helper.IsAdmin())
                {
                    ParsecVDD.SetCustomDisplayModes(modes);
                    ParsecVDD.SetParentGPU(parentGPU);
                }
                else
                {
                    var args = $"-custom \"{Display.DumpModes(modes)}\" \"{parentGPU}\"";
                    if (Helper.RunAdminTask(args) == false)
                    {
                        MessageBox.Show(App.GetTranslation("t_msg_custom_access_denied"),
                            App.NAME, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            Window.GetWindow(this)?.Close();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            TextBoxes = FindVisualChildren<TextBox>(this).ToArray();

            var modes = ParsecVDD.GetCustomDisplayModes();

            for (int i = 0, j = 0; i < 15 && j < modes.Count; i += 3, j++)
            {
                TextBoxes[i].Text = $"{modes[j].Width}";
                TextBoxes[i + 1].Text = $"{modes[j].Height}";
                TextBoxes[i + 2].Text = $"{modes[j].Hz}";
            }

            var parentGPU = ParsecVDD.GetParentGPU();
            xParentGPU.SelectedValue = parentGPU.ToString();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield return (T)Enumerable.Empty<T>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject ithChild = VisualTreeHelper.GetChild(depObj, i);
                if (ithChild == null) continue;
                if (ithChild is T t) yield return t;
                foreach (T childOfChild in FindVisualChildren<T>(ithChild)) yield return childOfChild;
            }
        }
    }
}