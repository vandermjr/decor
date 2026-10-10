using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
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

    public const string GenericSearchHelp = "Digite palavras para combinar termos (todos precisam corresponder). Use aspas para buscar uma frase exata, como \"piso vinílico\". Um número localiza pelo código. Apague o texto para limpar; use Pesquisar com o campo vazio para listar tudo.";
    public const string ProductSearchHelp = "Pesquise por código, descrição, marca ou referência. Combine palavras: piso arquitech (todos os termos precisam corresponder). Use aspas para uma frase: \"piso vinílico\". Estoque: com estoque; sem estoque; estoque negativo; estoque > 5; estoque < 10; estoque = 0; estoque acima de 5,5; estoque abaixo de 10. Combine, por exemplo: piso com estoque. Use Pesquisar com o campo vazio para listar todos os produtos.";

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
    public ICommand SearchActionCommand => _searchActionCommand;
    public ICommand ClearActionCommand => _clearActionCommand;

    public bool FocusTextInput() => SearchInput.Focus();

    public void ExecuteSearch()
    {
        _searchTimer.Stop();
        _waitingForSearch = false;
        if (SearchCommand?.CanExecute(null) == true)
            SearchCommand.Execute(null);
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
                if (_commandsAttached) _searchTimer.Start();
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
        if (change.Property == SearchHelpProperty)
            HasSearchHelp = !string.IsNullOrWhiteSpace(SearchHelp);
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