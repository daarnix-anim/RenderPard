---
name: "Windows Context Menu Registration"
description: "Best practices for dynamically registering and unregistering cascading context menus for video files in the Windows Registry using C#."
---

# Успешные методы работы с Контекстным Меню Windows (Registry)

В RenderPard реализована динамическая интеграция пресетов рендера в контекстное меню Windows Explorer (вызывается по клику правой кнопкой мыши на `.mp4`, `.mov`, и т.д.). При доработке `ContextMenuManager.cs` следует соблюдать следующие паттерны.

## 1. Регистрация каскадного меню
Мы используем `SystemFileAssociations`, чтобы меню отображалось для конкретных расширений, независимо от того, какой плеер установлен по умолчанию.
**Путь в реестре:** `Software\Classes\SystemFileAssociations\.mp4\shell\RenderPard`

### Обязательные ключи для родительского меню:
- `MUIVerb` = "RenderPard" (Название в меню).
- `Icon` = `"{exePath}"` (Использовать иконку исполняемого файла приложения).
- `SubCommands` = `""` (Обязательно пустое значение! Это говорит Windows, что подменю будут заданы в подкаталоге `\shell`).
- `MultiSelectModel` = `"Player"` (Позволяет выделять сразу несколько видеофайлов и запускать пакетную обработку без пропадания пункта из контекстного меню).

## 2. Иконки подменю (Пресеты)
Каждый пресет добавляется как подкаталог в `...\shell\RenderPard\shell\{id}_{SafeName}`.
Обеспечение наличия иконки обязательно, чтобы интерфейс выглядел законченным.
```csharp
string presetIconPath = Path.Combine(exeDir, $"icon_{safeName}.ico");

if (File.Exists(presetIconPath))
{
    // Если есть кастомная иконка для пресета
    presetKey.SetValue("Icon", $"\"{presetIconPath}\"");
}
else
{
    // FALLBACK: если своей иконки нет, наследуем иконку .exe
    presetKey.SetValue("Icon", $"\"{exePath}\"");
}
```
**Важно:** Избегайте использования путей к несуществующим `.ico` файлам (как `icon.ico`, если он не копируется в Output), иначе Windows покажет белый пустой квадрат.

## 3. Очистка перед обновлением
Поскольку пресеты могут меняться (название, удаление), **всегда очищайте** весь куст `\shell` перед его перестройкой, чтобы не оставлять «висячие» (orphan) пункты меню от удаленных пресетов.
```csharp
string subCommandsPath = basePath + @"\shell";
try
{
    Registry.CurrentUser.DeleteSubKeyTree(subCommandsPath, false);
}
catch { }
```
