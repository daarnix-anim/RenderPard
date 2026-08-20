# Folder Conversion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable batch file conversion by selecting a folder via Windows Context Menu, filtering which extensions to process through a UI dialog, and enqueueing the matched files.

**Architecture:** 
1. `ContextMenuManager` adds a new registry entry in `HKEY_CURRENT_USER\Software\Classes\Directory\shell` to support directory right-clicks with 3 category submenus.
2. A new `FolderImportDialog` allows the user to filter discovered extensions before adding them to the queue.
3. `App.xaml.cs` orchestrates detecting directory arguments, recursive scanning, invoking the dialog, and passing files to `MainWindow`.

**Tech Stack:** .NET 10, WPF, C#, Windows Registry

## Global Constraints
- **Platform:** Windows OS only
- **UI:** Must match Carbon Tech dark theme

---

### Task 1: Create FolderImportDialog & ViewModels

**Files:**
- Create: `RenderPard.UI/ViewModels/ExtensionItemViewModel.cs`
- Create: `RenderPard.UI/FolderImportDialog.xaml`
- Create: `RenderPard.UI/FolderImportDialog.xaml.cs`

**Interfaces:**
- Produces: `FolderImportDialog` with a constructor taking `Dictionary<string, int> extensions`, returning a `List<string> SelectedExtensions` property.

- [ ] **Step 1: Create ExtensionItemViewModel**
```csharp
namespace RenderPard.UI.ViewModels;

public partial class ExtensionItemViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _extension = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private int _count;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _isSelected = true;
}
```

- [ ] **Step 2: Create FolderImportDialog.xaml**
Implement a Window with `Style="{DynamicResource WindowStyle}"`.
Add a ListBox bound to `Extensions`, where each item is a CheckBox bound to `IsSelected`, displaying `Extension (Count)`.
Add Buttons: "Выбрать все", "Снять все", "Добавить в очередь", "Отмена".

- [ ] **Step 3: Create FolderImportDialog.xaml.cs**
```csharp
namespace RenderPard.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RenderPard.UI.ViewModels;

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

    private void SelectAll_Click(object sender, RoutedEventArgs e) { foreach(var ext in Extensions) ext.IsSelected = true; }
    private void DeselectAll_Click(object sender, RoutedEventArgs e) { foreach(var ext in Extensions) ext.IsSelected = false; }
    private void Add_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
```

- [ ] **Step 4: Verify Compilation**
Run: `dotnet build`
Expected: Build succeeds.

- [ ] **Step 5: Commit**
```bash
git add RenderPard.UI/ViewModels/ExtensionItemViewModel.cs RenderPard.UI/FolderImportDialog.xaml RenderPard.UI/FolderImportDialog.xaml.cs
git commit -m "feat: add FolderImportDialog for filtering extensions"
```

### Task 2: Update ContextMenuManager for Directories

**Files:**
- Modify: `RenderPard.Core/ContextMenuManager.cs`

**Interfaces:**
- Modifies: `RegisterSubMenu` and `UnregisterForExtension` to handle `Directory` extension type.

- [ ] **Step 1: Add Directory to SupportedExtensions array (conceptually)**
Wait, we should handle `Directory` explicitly because it's not a file extension.
Modify `ContextMenuManager.cs`:
In `Register(List<Preset> presets)`, add:
```csharp
RegisterDirectoryMenu(presets, exePath);
```

- [ ] **Step 2: Implement RegisterDirectoryMenu**
Create `RegisterDirectoryMenu(List<Preset> presets, string exePath)` inside `ContextMenuManager`.
Delete old `Software\Classes\Directory\shell\RenderPard`.
Create root key `Software\Classes\Directory\shell\RenderPard`, set `MUIVerb` to `RenderPard`, `SubCommands` to empty string.
Create submenus: `Video`, `Audio`, `Image` under `shell` key, similar to how it's done for files, but categorized by `preset.IsVideoPreset` etc.

- [ ] **Step 3: Update Unregister**
In `Unregister()`, add:
```csharp
try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\RenderPard", false); } catch { }
```

- [ ] **Step 4: Verify Compilation**
Run: `dotnet build`
Expected: Build succeeds.

- [ ] **Step 5: Commit**
```bash
git add RenderPard.Core/ContextMenuManager.cs
git commit -m "feat: add folder context menu registration"
```

### Task 3: Handle Directory Startup in App.xaml.cs

**Files:**
- Modify: `RenderPard.UI/App.xaml.cs`

**Interfaces:**
- Consumes: `FolderImportDialog`

- [ ] **Step 1: Detect Directory in ProcessArgs**
In `ProcessArgs`, inside `if (file != null)` block, check if `Directory.Exists(file)`.

- [ ] **Step 2: Scan Directory and group by extension**
```csharp
if (Directory.Exists(file))
{
    var supportedExts = new HashSet<string>(ContextMenuManager.SupportedExtensions, StringComparer.OrdinalIgnoreCase);
    var allFiles = Directory.EnumerateFiles(file, "*.*", SearchOption.AllDirectories)
                            .Where(f => supportedExts.Contains(Path.GetExtension(f)))
                            .ToList();
    
    if (allFiles.Count == 0)
    {
        MessageBox.Show("Не найдено поддерживаемых медиафайлов в этой папке.", "RenderPard", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }

    var extCounts = allFiles.GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
                            .ToDictionary(g => g.Key, g => g.Count());
    
    // Show Dialog
    Dispatcher.Invoke(() => {
        var dialog = new FolderImportDialog(extCounts);
        if (dialog.ShowDialog() == true)
        {
            var selectedExts = new HashSet<string>(dialog.SelectedExtensions, StringComparer.OrdinalIgnoreCase);
            var filesToAdd = allFiles.Where(f => selectedExts.Contains(Path.GetExtension(f))).ToList();
            
            if (MainWindow is MainWindow window)
            {
                foreach(var f in filesToAdd) window.EnqueueFile(f, preset);
                window.StartQueue();
                if (isSecondInstance) { window.WindowState = WindowState.Normal; window.Activate(); }
            }
        }
    });
}
else if (File.Exists(file))
{
    // Existing single file logic
    if (MainWindow is MainWindow window)
    {
        window.EnqueueFile(file, preset);
        window.StartQueue();
        if (isSecondInstance) { window.WindowState = WindowState.Normal; window.Activate(); }
    }
}
```

- [ ] **Step 3: Verify Compilation**
Run: `dotnet build`
Expected: Build succeeds.

- [ ] **Step 4: Commit**
```bash
git add RenderPard.UI/App.xaml.cs
git commit -m "feat: handle directory drag/drop and context menu selection"
```
