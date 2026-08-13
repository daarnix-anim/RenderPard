using System.Collections.Generic;
using System.Windows;

namespace RenderPard.UI
{
    public partial class IconPickerDialog : Window
    {
        public string SelectedIconName { get; private set; }

        public IconPickerDialog(IEnumerable<RenderPard.UI.ViewModels.IconItemViewModel> icons, string currentSelected)
        {
            InitializeComponent();
            IconsList.ItemsSource = icons;
            
            foreach (var icon in icons)
            {
                if (icon.Name == currentSelected)
                {
                    IconsList.SelectedItem = icon;
                    break;
                }
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (IconsList.SelectedItem is RenderPard.UI.ViewModels.IconItemViewModel selected)
            {
                SelectedIconName = selected.Name;
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Icon files (*.ico;*.png;*.jpg;*.jpeg;*.webp)|*.ico;*.png;*.jpg;*.jpeg;*.webp|All files (*.*)|*.*",
                Title = "Выберите иконку"
            };
            if (dialog.ShowDialog() == true)
            {
                SelectedIconName = dialog.FileName;
                DialogResult = true;
            }
        }
    }
}
