using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;

namespace Decor.AvaloniaUI.Controls;

public partial class DecorSearchField : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<DecorSearchField, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<ICommand?> SearchCommandProperty =
        AvaloniaProperty.Register<DecorSearchField, ICommand?>(nameof(SearchCommand));
    public static readonly StyledProperty<ICommand?> ClearCommandProperty =
        AvaloniaProperty.Register<DecorSearchField, ICommand?>(nameof(ClearCommand));
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<DecorSearchField, string?>(nameof(PlaceholderText));
    public static readonly StyledProperty<string?> SearchHelpProperty =
        AvaloniaProperty.Register<DecorSearchField, string?>(nameof(SearchHelp), GenericSearchHelp);
    public static readonly StyledProperty<bool> HasClearCommandProperty =
        AvaloniaProperty.Register<DecorSearchField, bool>(nameof(HasClearCommand));
    public static readonly StyledProperty<bool> HasSearchHelpProperty =
        AvaloniaProperty.Register<DecorSearchField, bool>(nameof(HasSearchHelp), true);

    public const string GenericSearchHelp = "Digite palavras para combinar termos, um código para localizar um registro ou uma frase entre aspas para manter as palavras juntas. Pesquisa vazia lista todos os registros.";
    public const string ProductSearchHelp = "Pesquise por código, descrição, marca ou referência. Combine palavras: piso arquitech. Use aspas: \"piso vinílico\". Estoque: com estoque; sem estoque; estoque negativo; estoque > 5; estoque < 10; estoque = 0; estoque acima de 5,5; estoque abaixo de 10. Combine: piso com estoque. Pesquisa vazia lista todos os produtos.";

    public DecorSearchField()
    {
        InitializeComponent();
        HelpText.Text = SearchHelp;
        SearchInput.KeyDown += SearchInput_KeyDown;
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public ICommand? SearchCommand { get => GetValue(SearchCommandProperty); set => SetValue(SearchCommandProperty, value); }
    public ICommand? ClearCommand { get => GetValue(ClearCommandProperty); set => SetValue(ClearCommandProperty, value); }
    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public string? SearchHelp { get => GetValue(SearchHelpProperty); set => SetValue(SearchHelpProperty, value); }
    public bool HasClearCommand { get => GetValue(HasClearCommandProperty); private set => SetValue(HasClearCommandProperty, value); }
    public bool HasSearchHelp { get => GetValue(HasSearchHelpProperty); private set => SetValue(HasSearchHelpProperty, value); }

    public bool FocusTextInput() => SearchInput.Focus();

    public void ExecuteSearch()
    {
        if (SearchCommand?.CanExecute(null) == true)
            SearchCommand.Execute(null);
    }

    private void SearchInput_KeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter)
        {
            ExecuteSearch();
            args.Handled = true;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ClearCommandProperty)
            HasClearCommand = ClearCommand is not null;
        if (change.Property == SearchHelpProperty)
        {
            HasSearchHelp = !string.IsNullOrWhiteSpace(SearchHelp);
            if (HelpText is not null)
                HelpText.Text = SearchHelp;
        }
    }
}