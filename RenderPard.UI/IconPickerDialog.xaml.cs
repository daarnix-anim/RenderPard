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
                try
                {
                    string customIconsDir = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                        "RenderPard", "CustomIcons");
                    if (!System.IO.Directory.Exists(customIconsDir))
                    {
                        System.IO.Directory.CreateDirectory(customIconsDir);
                    }

                    string fileName = System.IO.Path.GetFileName(dialog.FileName);
                    string destPath = System.IO.Path.Combine(customIconsDir, fileName);
                    System.IO.File.Copy(dialog.FileName, destPath, true);

                    SelectedIconName = destPath;
                    DialogResult = true;
                }
                catch
                {
                    SelectedIconName = dialog.FileName;
                    DialogResult = true;
                }
            }
        }
    }
}
