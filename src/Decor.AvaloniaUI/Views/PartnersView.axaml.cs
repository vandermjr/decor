using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Decor.AvaloniaUI.ViewModels;
using Decor.Core.DTOs;

namespace Decor.AvaloniaUI.Views;

public partial class PartnersView : UserControl
{
    public PartnersView()
    {
        InitializeComponent();
        PartnersGrid.InitializeColumns(
            dtoType: typeof(PartnerDTO),
            getDisplayName: propertyName => propertyName switch
            {
                nameof(PartnerDTO.PartnerID) => "Código",
                nameof(PartnerDTO.Name) => "Nome",
                nameof(PartnerDTO.Document) => "Documento",
                nameof(PartnerDTO.Phone) => "Telefone",
                nameof(PartnerDTO.PartnerType) => "Tipo",
                nameof(PartnerDTO.IsActive) => "Estado",
                _ => propertyName
            },
            getColumnWidth: propertyName => propertyName == nameof(PartnerDTO.Name)
                ? new DataGridLength(1, DataGridLengthUnitType.Star)
                : new DataGridLength(140));
        PartnersGrid.ValueMatchChanged += (_, args) =>
        {
            if (DataContext is PartnersViewModel viewModel)
                viewModel.SetValueMatch(args.ColumnName, args.MatchCount);
        };
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchTextBox.FocusTextInput());
    }

    public PartnersView(PartnersViewModel viewModel) : this()
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }
}