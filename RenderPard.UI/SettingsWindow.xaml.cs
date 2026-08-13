using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;

namespace RenderPard.UI
{
    public partial class SettingsWindow : Window
    {
        private Point _startPoint;

        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PresetsListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
        }

        private void PresetsListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point position = e.GetPosition(null);
                if (Math.Abs(position.X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is ListBox listBox)
                    {
                        var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                        if (listBoxItem != null)
                        {
                            var data = listBox.ItemContainerGenerator.ItemFromContainer(listBoxItem);
                            DataObject dragData = new DataObject("preset", data);
                            DragDrop.DoDragDrop(listBoxItem, dragData, DragDropEffects.Move);
                        }
                    }
                }
            }
        }

        private void PresetsListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("preset"))
            {
                var data = e.Data.GetData("preset");
                if (sender is ListBox listBox && listBox.DataContext is ViewModels.SettingsViewModel viewModel)
                {
                    var dropTarget = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                    if (dropTarget != null)
                    {
                        var targetData = listBox.ItemContainerGenerator.ItemFromContainer(dropTarget);
                        if (data != targetData && data is Core.Models.Preset sourcePreset && targetData is Core.Models.Preset targetPreset)
                        {
                            int sourceIndex = viewModel.Presets.IndexOf(sourcePreset);
                            int targetIndex = viewModel.Presets.IndexOf(targetPreset);
                            
                            if (sourceIndex >= 0 && targetIndex >= 0)
                            {
                                viewModel.Presets.Move(sourceIndex, targetIndex);
                                viewModel.SelectedPreset = sourcePreset;
                            }
                        }
                    }
                }
            }
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t)
                {
                    return t;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
