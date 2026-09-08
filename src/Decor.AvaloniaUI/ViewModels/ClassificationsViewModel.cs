using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Decor.Core.DTOs;
using Decor.Core.Interfaces.Services;

namespace Decor.AvaloniaUI.ViewModels;

public sealed class ClassificationsViewModel : IStatusBarSource
{
    private readonly IClassService _classService;
    private readonly IFamilyService _familyService;
    private readonly IGroupService _groupService;
    private readonly ISubgroupService _subgroupService;

    private ClassDTO? _selectedClass;
    private FamilyDTO? _selectedFamily;
    private GroupDTO? _selectedGroup;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public ClassificationsViewModel(
        IClassService classService,
        IFamilyService familyService,
        IGroupService groupService,
        ISubgroupService subgroupService)
    {
        _classService = classService;
        _familyService = familyService;
        _groupService = groupService;
        _subgroupService = subgroupService;
    }

    public ObservableCollection<ClassDTO> Classes { get; } = [];
    public ObservableCollection<FamilyDTO> Families { get; } = [];
    public ObservableCollection<GroupDTO> Groups { get; } = [];
    public ObservableCollection<SubgroupDTO> Subgroups { get; } = [];

    public ClassDTO? SelectedClass
    {
        get => _selectedClass;
        set
        {
            if (SetField(ref _selectedClass, value))
            {
                _ = LoadFamiliesAsync();
            }
        }
    }

    public FamilyDTO? SelectedFamily
    {
        get => _selectedFamily;
        set
        {
            if (SetField(ref _selectedFamily, value))
            {
                _ = LoadGroupsAsync();
            }
        }
    }

    public GroupDTO? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (SetField(ref _selectedGroup, value))
            {
                _ = LoadSubgroupsAsync();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string? StatusPrimary => null;
    public string? StatusSecondary => null;
    public bool HasStatusPrimary => false;
    public bool HasStatusSecondary => false;
    public string? PaginationStatus => null;
    public string? PaginationPageStatus => null;
    public bool HasPagination => false;
    public System.Windows.Input.ICommand? PreviousPageCommand => null;
    public System.Windows.Input.ICommand? NextPageCommand => null;
    public System.Windows.Input.ICommand? FirstPageCommand => null;
    public System.Windows.Input.ICommand? LastPageCommand => null;
    public bool HasPreviousPage => false;
    public bool HasNextPage => false;
    public bool HasFirstPage => false;
    public bool HasLastPage => false;
    public IReadOnlyList<int> PageSizeOptions => [];
    public int SelectedPageSize { get => 0; set { } }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public async Task InitializeAsync()
    {
        await LoadClassesAsync();
    }

    private async Task LoadClassesAsync()
    {
        IsBusy = true;
        try
        {
            Replace(Classes, await _classService.GetAllAsync());
            SelectedClass = null;
            Families.Clear();
            Groups.Clear();
            Subgroups.Clear();
            StatusMessage = $"{Classes.Count} classe(s) carregada(s).";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar as classes.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadFamiliesAsync()
    {
        if (SelectedClass is null)
        {
            Families.Clear();
            return;
        }

        IsBusy = true;
        try
        {
            Replace(Families, await _familyService.GetByClassIdAsync(SelectedClass.ClassID));
            SelectedFamily = null;
            Groups.Clear();
            Subgroups.Clear();
            StatusMessage = $"{Families.Count} família(s) em {SelectedClass.ClassName}.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar as famílias.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadGroupsAsync()
    {
        if (SelectedFamily is null)
        {
            Groups.Clear();
            return;
        }

        IsBusy = true;
        try
        {
            Replace(Groups, await _groupService.GetByFamilyIdAsync(SelectedFamily.FamilyID));
            SelectedGroup = null;
            Subgroups.Clear();
            StatusMessage = $"{Groups.Count} grupo(s) em {SelectedFamily.FamilyName}.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar os grupos.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSubgroupsAsync()
    {
        if (SelectedGroup is null)
        {
            Subgroups.Clear();
            return;
        }

        IsBusy = true;
        try
        {
            Replace(Subgroups, await _subgroupService.GetByGroupIdAsync(SelectedGroup.GroupID));
            StatusMessage = $"{Subgroups.Count} subgrupo(s) em {SelectedGroup.GroupName}.";
        }
        catch (Exception)
        {
            StatusMessage = "Não foi possível carregar os subgrupos.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
