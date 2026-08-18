using System;
using System.Diagnostics;
using System.Windows;

namespace RenderPard.UI
{
    public partial class WhatsNewDialog : Window
    {
        private readonly string _version;

        public WhatsNewDialog(string version = "1.5.8")
        {
            InitializeComponent();
            _version = version;
            VersionBadgeText.Text = $"v{version}";
            TitleHeaderText.Text = $"{FindResource("WhatsNewTitle")} v{version}";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnGitHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string tag = _version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? _version : $"v{_version}";
                string url = $"https://github.com/daarnix-anim/RenderPard/releases/tag/{tag}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть страницу в браузере: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
