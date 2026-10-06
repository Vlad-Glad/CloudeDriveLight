using System.Collections.ObjectModel;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace GDriveLight.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public string Title => "GDriveLight";

    public ObservableCollection<FileItemViewModel> Files { get; }

    private FileItemViewModel? _selectedFile;
    public FileItemViewModel? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    public MainWindowViewModel()
    {
        Files = new ObservableCollection<FileItemViewModel>
        {
            new FileItemViewModel { Name = "Documents", DateModified = "10/06/2026", Size = "—", Icon = SymbolRegular.Folder24, IconColorBrushKey = "#5F6368" },
            new FileItemViewModel { Name = "Photos", DateModified = "10/05/2026", Size = "—", Icon = SymbolRegular.Folder24, IconColorBrushKey = "#5F6368" },
            new FileItemViewModel { Name = "Project Plan.docx", DateModified = "10/06/2026", Size = "124 KB", Icon = SymbolRegular.Document24, IconColorBrushKey = "#185ABD" },
            new FileItemViewModel { Name = "Budget.xlsx", DateModified = "10/06/2026", Size = "86 KB", Icon = SymbolRegular.Table24, IconColorBrushKey = "#107C41" },
            new FileItemViewModel { Name = "Presentation.pdf", DateModified = "10/05/2026", Size = "2.4 MB", Icon = SymbolRegular.DocumentPdf24, IconColorBrushKey = "#EA4335" },
            new FileItemViewModel { Name = "Photo_2026.jpg", DateModified = "10/04/2026", Size = "3.8 MB", Icon = SymbolRegular.Image24, IconColorBrushKey = "#A142F4" },
            new FileItemViewModel { Name = "Video.mp4", DateModified = "10/03/2026", Size = "128 MB", Icon = SymbolRegular.Video24, IconColorBrushKey = "#EA4335" },
            new FileItemViewModel { Name = "Notes.txt", DateModified = "10/02/2026", Size = "4 KB", Icon = SymbolRegular.DocumentText24, IconColorBrushKey = "#5F6368" }
        };

        SelectedFile = Files.FirstOrDefault(f => f.Name.Contains(".docx"));
    }
}
