using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;

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
    public static readonly StyledProperty<bool> SearchOnTextChangedProperty =
        AvaloniaProperty.Register<DecorSearchField, bool>(nameof(SearchOnTextChanged), true);
    public static readonly StyledProperty<bool> ShowSearchButtonProperty =
        AvaloniaProperty.Register<DecorSearchField, bool>(nameof(ShowSearchButton), true);
    public static readonly StyledProperty<bool> ShowFilterButtonProperty =
        AvaloniaProperty.Register<DecorSearchField, bool>(nameof(ShowFilterButton), true);
    public static readonly StyledProperty<bool> ShowLeadingSearchIconProperty =
        AvaloniaProperty.Register<DecorSearchField, bool>(nameof(ShowLeadingSearchIcon));
    public static readonly StyledProperty<bool> HighlightActionButtonsProperty =
        AvaloniaProperty.Register<DecorSearchField, bool>(nameof(HighlightActionButtons));
    public static readonly StyledProperty<Thickness> FieldBorderThicknessProperty =
        AvaloniaProperty.Register<DecorSearchField, Thickness>(nameof(FieldBorderThickness), new Thickness(1));
    public static readonly StyledProperty<FlyoutBase?> FilterFlyoutProperty =
        AvaloniaProperty.Register<DecorSearchField, FlyoutBase?>(nameof(FilterFlyout));

    public const string GenericSearchHelp = "Digite palavras para combinar termos (todos precisam corresponder). Use aspas para buscar uma frase exata, como \"piso vinílico\". Um número localiza pelo código. Apague o texto para limpar; use Pesquisar com o campo vazio para listar tudo.";
    public const string ProductSearchHelp = "Pesquise por código, descrição, marca ou referência. Combine palavras: piso arquitech (todos os termos precisam corresponder). Use aspas para uma frase: \"piso vinílico\". Estoque: com estoque; sem estoque; estoque negativo; estoque > 5; estoque < 10; estoque = 0; estoque acima de 5,5; estoque abaixo de 10. Combine, por exemplo: piso com estoque. Pressione Enter com o campo vazio para listar todos os produtos.";

    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly SearchCommandProxy _searchActionCommand;
    private readonly SearchCommandProxy _clearActionCommand;
    private bool _commandsAttached;
    private bool _waitingForSearch;
    private bool _waitingForClear;
    private bool _executingClear;

    public DecorSearchField()
    {
        _searchActionCommand = new SearchCommandProxy(this, isClear: false);
        _clearActionCommand = new SearchCommandProxy(this, isClear: true);
        InitializeComponent();
        UpdateActionButtonHighlight();
        SearchInput.KeyDown += SearchInput_KeyDown;
        _searchTimer.Tick += SearchTimer_Tick;
        AttachedToVisualTree += (_, _) => AttachCommands();
        DetachedFromVisualTree += (_, _) => DetachCommands();
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public ICommand? SearchCommand { get => GetValue(SearchCommandProperty); set => SetValue(SearchCommandProperty, value); }
    public ICommand? ClearCommand { get => GetValue(ClearCommandProperty); set => SetValue(ClearCommandProperty, value); }
    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public string? SearchHelp { get => GetValue(SearchHelpProperty); set => SetValue(SearchHelpProperty, value); }
    public bool HasClearCommand { get => GetValue(HasClearCommandProperty); private set => SetValue(HasClearCommandProperty, value); }
    public bool HasSearchHelp { get => GetValue(HasSearchHelpProperty); private set => SetValue(HasSearchHelpProperty, value); }
    public bool SearchOnTextChanged { get => GetValue(SearchOnTextChangedProperty); set => SetValue(SearchOnTextChangedProperty, value); }
    public bool ShowSearchButton { get => GetValue(ShowSearchButtonProperty); set => SetValue(ShowSearchButtonProperty, value); }
    public bool ShowFilterButton { get => GetValue(ShowFilterButtonProperty); set => SetValue(ShowFilterButtonProperty, value); }
    public bool ShowLeadingSearchIcon { get => GetValue(ShowLeadingSearchIconProperty); set => SetValue(ShowLeadingSearchIconProperty, value); }
    public bool HighlightActionButtons { get => GetValue(HighlightActionButtonsProperty); set => SetValue(HighlightActionButtonsProperty, value); }
    public Thickness FieldBorderThickness { get => GetValue(FieldBorderThicknessProperty); set => SetValue(FieldBorderThicknessProperty, value); }
    public FlyoutBase? FilterFlyout { get => GetValue(FilterFlyoutProperty); set => SetValue(FilterFlyoutProperty, value); }
    public ICommand SearchActionCommand => _searchActionCommand;
    public ICommand ClearActionCommand => _clearActionCommand;

    public bool FocusTextInput() => SearchInput.Focus();

    public void ExecuteSearch()
    {
        _searchTimer.Stop();
        if (SearchCommand?.CanExecute(null) == true)
        {
            _waitingForSearch = false;
            SearchCommand.Execute(null);
        }
        else
            _waitingForSearch = SearchCommand is not null;
    }

    private void ExecuteClear()
    {
        _searchTimer.Stop();
        _waitingForSearch = false;
        if (ClearCommand?.CanExecute(null) != true)
        {
            _waitingForClear = ClearCommand is not null;
            return;
        }

        _waitingForClear = false;
        _executingClear = true;
        try { ClearCommand.Execute(null); }
        finally { _executingClear = false; }
    }

    private void SearchTimer_Tick(object? sender, EventArgs args)
    {
        _searchTimer.Stop();
        if (SearchCommand?.CanExecute(null) == true)
        {
            _waitingForSearch = false;
            SearchCommand.Execute(null);
        }
        else
            _waitingForSearch = true;
    }

    private void AttachCommands()
    {
        if (_commandsAttached) return;
        _commandsAttached = true;
        if (SearchCommand is { } searchCommand) searchCommand.CanExecuteChanged += OnCommandCanExecuteChanged;
        if (ClearCommand is { } clearCommand) clearCommand.CanExecuteChanged += OnCommandCanExecuteChanged;
        if (_waitingForClear) ExecuteClear();
        else if (_waitingForSearch && SearchCommand?.CanExecute(null) == true) _searchTimer.Start();
    }

    private void DetachCommands()
    {
        if (!_commandsAttached) return;
        _commandsAttached = false;
        _searchTimer.Stop();
        _waitingForSearch = false;
        _waitingForClear = false;
        if (SearchCommand is { } searchCommand) searchCommand.CanExecuteChanged -= OnCommandCanExecuteChanged;
        if (ClearCommand is { } clearCommand) clearCommand.CanExecuteChanged -= OnCommandCanExecuteChanged;
    }

    private void OnCommandCanExecuteChanged(object? sender, EventArgs args)
    {
        _searchActionCommand.RaiseCanExecuteChanged();
        _clearActionCommand.RaiseCanExecuteChanged();
        if (_waitingForClear && ClearCommand?.CanExecute(null) == true)
            ExecuteClear();
        else if (_waitingForSearch && SearchCommand?.CanExecute(null) == true)
            _searchTimer.Start();
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
        if (change.Property == TextProperty)
        {
            _searchTimer.Stop();
            if (_executingClear) return;

            var text = change.NewValue as string;
            if (string.IsNullOrWhiteSpace(text))
            {
                _waitingForSearch = false;
                if (!string.IsNullOrWhiteSpace(change.OldValue as string) && _commandsAttached)
                {
                    _waitingForClear = true;
                    ExecuteClear();
                }
            }
            else
            {
                _waitingForClear = false;
                if (_commandsAttached && SearchOnTextChanged) _searchTimer.Start();
            }
        }
        else if (change.Property == SearchCommandProperty)
        {
            if (_commandsAttached)
            {
                if (change.OldValue is ICommand previous) previous.CanExecuteChanged -= OnCommandCanExecuteChanged;
                if (change.NewValue is ICommand current) current.CanExecuteChanged += OnCommandCanExecuteChanged;
            }
            _searchActionCommand.RaiseCanExecuteChanged();
        }
        else if (change.Property == ClearCommandProperty)
        {
            if (_commandsAttached)
            {
                if (change.OldValue is ICommand previous) previous.CanExecuteChanged -= OnCommandCanExecuteChanged;
                if (change.NewValue is ICommand current) current.CanExecuteChanged += OnCommandCanExecuteChanged;
            }
            HasClearCommand = ClearCommand is not null;
            _clearActionCommand.RaiseCanExecuteChanged();
        }
        else if (change.Property == HighlightActionButtonsProperty)
            UpdateActionButtonHighlight();
        if (change.Property == SearchHelpProperty)
            HasSearchHelp = !string.IsNullOrWhiteSpace(SearchHelp);
    }

    private void UpdateActionButtonHighlight()
    {
        ClearButton.Classes.Set("decor-action", HighlightActionButtons);
        InfoButton.Classes.Set("decor-action", HighlightActionButtons);
        ClearButton.Classes.Set("highlighted-search-action", HighlightActionButtons);
        InfoButton.Classes.Set("highlighted-search-action", HighlightActionButtons);
    }

    private sealed class SearchCommandProxy(DecorSearchField owner, bool isClear) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
            => (isClear ? owner.ClearCommand : owner.SearchCommand)?.CanExecute(parameter) == true;

        public void Execute(object? parameter)
        {
            if (isClear) owner.ExecuteClear();
            else owner.ExecuteSearch();
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
