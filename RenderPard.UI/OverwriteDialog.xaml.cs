using System.Windows;

namespace RenderPard.UI
{
    public enum OverwriteResult { Yes, YesToAll, No, NoToAll }

    public partial class OverwriteDialog : Window
    {
        public OverwriteResult Result { get; private set; } = OverwriteResult.No;

        public OverwriteDialog(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e) { Result = OverwriteResult.Yes; DialogResult = true; }
        private void BtnYesToAll_Click(object sender, RoutedEventArgs e) { Result = OverwriteResult.YesToAll; DialogResult = true; }
        private void BtnNo_Click(object sender, RoutedEventArgs e) { Result = OverwriteResult.No; DialogResult = false; }
        private void BtnNoToAll_Click(object sender, RoutedEventArgs e) { Result = OverwriteResult.NoToAll; DialogResult = false; }
    }
}
