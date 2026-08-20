using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RenderPard.UI.ViewModels;

namespace RenderPard.UI;

public partial class FolderImportDialog : Window
{
    public List<ExtensionItemViewModel> Extensions { get; set; } = new();
    public List<string> SelectedExtensions => Extensions.Where(e => e.IsSelected).Select(e => e.Extension).ToList();

    public FolderImportDialog(Dictionary<string, int> extCounts)
    {
        InitializeComponent();
        
        foreach (var kvp in extCounts.OrderByDescending(x => x.Value))
        {
            Extensions.Add(new ExtensionItemViewModel { Extension = kvp.Key, Count = kvp.Value });
        }
        
        DataContext = this;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) 
    { 
        foreach(var ext in Extensions) ext.IsSelected = true; 
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e) 
    { 
        foreach(var ext in Extensions) ext.IsSelected = false; 
    }

    private void Add_Click(object sender, RoutedEventArgs e) 
    { 
        DialogResult = true; 
        Close(); 
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) 
    { 
        DialogResult = false; 
        Close(); 
    }
}
