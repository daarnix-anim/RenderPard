using CommunityToolkit.Mvvm.ComponentModel;

namespace RenderPard.UI.ViewModels;

public partial class ExtensionItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _extension = "";

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _isSelected = true;
}
