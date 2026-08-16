using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using RenderPard.Core.Models;

namespace RenderPard.UI
{
    public partial class SettingsWindow : Window
    {
        private Point _dragStartPoint;
        private bool _isDragging;
        private ListBoxItem? _draggedItemContainer;
        private Preset? _draggedPreset;
        private Border? _currentTopIndicator;
        private Border? _currentBottomIndicator;

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
            // Ignore drag initiation if the user clicked directly on a CheckBox or Button
            if (FindAncestor<CheckBox>((DependencyObject)e.OriginalSource) != null ||
                FindAncestor<Button>((DependencyObject)e.OriginalSource) != null)
            {
                _draggedPreset = null;
                _draggedItemContainer = null;
                return;
            }

            _dragStartPoint = e.GetPosition(null);
            _draggedItemContainer = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (_draggedItemContainer != null && sender is ListBox listBox)
            {
                _draggedPreset = listBox.ItemContainerGenerator.ItemFromContainer(_draggedItemContainer) as Preset;
            }
            else
            {
                _draggedPreset = null;
                _draggedItemContainer = null;
            }
        }

        private void PresetsListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedPreset == null || _draggedItemContainer == null || _isDragging)
                return;

            Point currentPoint = e.GetPosition(null);
            if (Math.Abs(currentPoint.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(currentPoint.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isDragging = true;
                try
                {
                    DataObject dragData = new DataObject("preset", _draggedPreset);
                    DragDrop.DoDragDrop(_draggedItemContainer, dragData, DragDropEffects.Move);
                }
                catch { }
                finally
                {
                    _isDragging = false;
                    _draggedPreset = null;
                    _draggedItemContainer = null;
                    ClearDropIndicators();
                }
            }
        }

        private void PresetsListBox_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("preset"))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                ClearDropIndicators();
                return;
            }

            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (targetItem == null || sender is not ListBox listBox)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                ClearDropIndicators();
                return;
            }

            var sourcePreset = e.Data.GetData("preset") as Preset;
            var targetPreset = listBox.ItemContainerGenerator.ItemFromContainer(targetItem) as Preset;

            if (sourcePreset == null || targetPreset == null || ReferenceEquals(sourcePreset, targetPreset))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                ClearDropIndicators();
                return;
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            Point pos = e.GetPosition(targetItem);
            bool isTop = pos.Y < (targetItem.ActualHeight / 2.0);

            UpdateDropIndicators(targetItem, isTop);
        }

        private void PresetsListBox_DragLeave(object sender, DragEventArgs e)
        {
            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (targetItem == null)
            {
                ClearDropIndicators();
            }
        }

        private void PresetsListBox_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent("preset") && sender is ListBox listBox && listBox.DataContext is ViewModels.SettingsViewModel viewModel)
                {
                    var sourcePreset = e.Data.GetData("preset") as Preset;
                    var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                    if (targetItem != null && sourcePreset != null)
                    {
                        var targetPreset = listBox.ItemContainerGenerator.ItemFromContainer(targetItem) as Preset;
                        if (targetPreset != null && !ReferenceEquals(sourcePreset, targetPreset))
                        {
                            Point pos = e.GetPosition(targetItem);
                            bool insertAfter = pos.Y >= (targetItem.ActualHeight / 2.0);

                            viewModel.MovePreset(sourcePreset, targetPreset, insertAfter);
                        }
                    }
                }
            }
            finally
            {
                ClearDropIndicators();
            }
        }

        private void UpdateDropIndicators(ListBoxItem targetItem, bool isTop)
        {
            var topInd = targetItem.Template.FindName("TopDropIndicator", targetItem) as Border;
            var btmInd = targetItem.Template.FindName("BottomDropIndicator", targetItem) as Border;

            if (_currentTopIndicator != null && _currentTopIndicator != topInd)
                _currentTopIndicator.Visibility = Visibility.Collapsed;
            if (_currentBottomIndicator != null && _currentBottomIndicator != btmInd)
                _currentBottomIndicator.Visibility = Visibility.Collapsed;

            _currentTopIndicator = topInd;
            _currentBottomIndicator = btmInd;

            if (isTop)
            {
                if (topInd != null) topInd.Visibility = Visibility.Visible;
                if (btmInd != null) btmInd.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (topInd != null) topInd.Visibility = Visibility.Collapsed;
                if (btmInd != null) btmInd.Visibility = Visibility.Visible;
            }
        }

        private void ClearDropIndicators()
        {
            if (_currentTopIndicator != null)
            {
                _currentTopIndicator.Visibility = Visibility.Collapsed;
                _currentTopIndicator = null;
            }
            if (_currentBottomIndicator != null)
            {
                _currentBottomIndicator.Visibility = Visibility.Collapsed;
                _currentBottomIndicator = null;
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
