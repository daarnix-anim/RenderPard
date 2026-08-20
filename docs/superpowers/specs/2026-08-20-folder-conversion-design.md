# Folder Conversion Feature Design

## Purpose
Allow users to select a folder via the Windows context menu, pick a preset, and then filter which file formats within that folder (and its subfolders) should be converted. This solves the problem of mixed-format directories where a user only wants to process specific file types.

## Architecture & Components

### 1. Windows Registry Integration (`ContextMenuManager`)
- **New Target**: Register the application in `HKEY_CURRENT_USER\Software\Classes\Directory\shell`.
- **Menu Structure**: A single root menu item `"RenderPard"` for directories.
- **Submenus**: Under the root menu, create three submenus: `Video`, `Audio`, and `Image`.
- **Items**: Place the corresponding presets inside these submenus.
- **Action**: When a preset is clicked, launch the app with arguments: `--file "C:\Selected\Folder" --preset "PresetName"`.

### 2. Startup Processing (`App.xaml.cs`)
- When receiving `--file <path>`, check if `<path>` is a directory or a file.
- If it's a file, proceed with the existing enqueue logic.
- If it's a directory:
  - Recursively scan the directory for all files that have an extension in `SupportedExtensions`.
  - Group by extension and count occurrences.
  - If no supported files are found, show a message box and exit/return.
  - Otherwise, open the `FolderImportDialog`.

### 3. Folder Import UI (`FolderImportDialog.xaml`)
- **Type**: A modal WPF window (`Window`).
- **Style**: Carbon Tech dark theme, matching the rest of the application.
- **Data Context**: A ViewModel containing a list of discovered extensions and their counts (e.g., `ExtensionItemViewModel` with `Name`, `Count`, `IsSelected`).
- **Default State**: All found extensions are checked by default.
- **Controls**:
  - "Select All" / "Deselect All" buttons.
  - "Add to Queue" button (accepts the dialog).
  - "Cancel" button.
- **Output**: Returns the list of selected extensions.

### 4. Queue Integration (`MainWindow` / `QueueViewModel`)
- After the dialog returns with selected extensions, filter the previously scanned files to only those matching the selected extensions.
- Iterate through the filtered files and call the existing `EnqueueFile(file, preset)` logic.
- Start the queue.

## Data Flow
1. **User** right-clicks `Folder A` -> `RenderPard` -> `Video` -> `MP4 to GIF`.
2. **Windows** executes `RenderPard.exe --file "Folder A" --preset "MP4 to GIF"`.
3. **App.xaml.cs** detects `Folder A` is a directory.
4. **App.xaml.cs** scans `Folder A` -> finds 10 `.mp4`, 5 `.png`.
5. **FolderImportDialog** opens -> User unchecks `.png` (leaves `.mp4` checked) -> clicks "Add to Queue".
6. **App.xaml.cs** filters list to only 10 `.mp4` files.
7. **MainWindow** enqueues the 10 `.mp4` files with the "MP4 to GIF" preset.
8. **Render Queue** begins processing.

## Error Handling
- If the directory scan fails due to permissions (`UnauthorizedAccessException`), skip the inaccessible subfolders or log the error.
- If no files are found in the directory, inform the user and close gracefully.

## Scope & Constraints
- Focus only on folder context menu and directory scanning.
- Keep the UI consistent with the existing theme.
- Ensure the context menu registration code cleans up old registry keys if necessary to avoid clutter.
