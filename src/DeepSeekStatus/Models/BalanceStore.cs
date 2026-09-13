using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using DeepSeekStatus.Support;
using Microsoft.Win32;

namespace DeepSeekStatus.Models;

public sealed class BalanceStore : INotifyPropertyChanged
{
    public enum BalanceState
    {
        NoKey,
        Loading,
        Loaded,
        Failed,
    }

    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly DispatcherTimer _timer;
    private string? _apiKey;
    private int _refreshToken;
    private bool _isRefreshing;
    private bool _hasKey;
    private bool _isEditingKey;
    private string _keyDraft = string.Empty;
    private string? _keyError;
    private BalanceState _state = BalanceState.NoKey;
    private DeepSeekBalance? _balance;
    private string? _errorMessage;
    private bool _errorSuggestsReplacingKey;
    private DateTimeOffset? _lastRefreshed;

    public BalanceStore()
    {
        _timer = new DispatcherTimer { Interval = RefreshInterval };
        _timer.Tick += (_, _) => Refresh();

        SystemEvents.PowerModeChanged += (_, args) =>
        {
            if (args.Mode != PowerModes.Resume)
            {
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                RefreshIfNeeded();
            }
            else
            {
                dispatcher.BeginInvoke(RefreshIfNeeded);
            }
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public BalanceState State
    {
        get => _state;
        private set => Set(ref _state, value);
    }

    public DeepSeekBalance? Balance
    {
        get => _balance;
        private set => Set(ref _balance, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => Set(ref _errorMessage, value);
    }

    public bool ErrorSuggestsReplacingKey
    {
        get => _errorSuggestsReplacingKey;
        private set => Set(ref _errorSuggestsReplacingKey, value);
    }

    public bool HasKey
    {
        get => _hasKey;
        private set => Set(ref _hasKey, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => Set(ref _isRefreshing, value);
    }

    public bool IsEditingKey
    {
        get => _isEditingKey;
        private set => Set(ref _isEditingKey, value);
    }

    public string KeyDraft
    {
        get => _keyDraft;
        set => Set(ref _keyDraft, value);
    }

    public string? KeyError
    {
        get => _keyError;
        private set => Set(ref _keyError, value);
    }

    public DateTimeOffset? LastRefreshed
    {
        get => _lastRefreshed;
        private set => Set(ref _lastRefreshed, value);
    }

    public void Start()
    {
        ReloadKey();
        _timer.Start();
    }

    public void Refresh()
    {
        StartRefresh(force: false);
    }

    public void RefreshIfNeeded()
    {
        if (!HasKey || IsRefreshing)
        {
            return;
        }

        if (LastRefreshed is null || DateTimeOffset.UtcNow - LastRefreshed.Value >= RefreshInterval)
        {
            Refresh();
        }
    }

    public void BeginEditingKey()
    {
        KeyDraft = _apiKey ?? string.Empty;
        KeyError = null;
        IsEditingKey = true;
    }

    public void CancelEditingKey()
    {
        IsEditingKey = false;
        KeyDraft = string.Empty;
        KeyError = null;
    }

    public void SaveKey()
    {
        var trimmed = KeyDraft.Trim();
        var error = CredentialManager.Save(trimmed);
        if (error is not null)
        {
            KeyError = string.Format(Strings.Get("balance.key.saveFailed"), error);
            return;
        }

        _apiKey = trimmed;
        HasKey = true;
        IsEditingKey = false;
        KeyDraft = string.Empty;
        KeyError = null;
        StartRefresh(force: true);
    }

    public void RemoveKey()
    {
        CredentialManager.Delete();
        _apiKey = null;
        HasKey = false;
        IsEditingKey = false;
        KeyDraft = string.Empty;
        KeyError = null;
        State = BalanceState.NoKey;
        Balance = null;
        ErrorMessage = null;
        ErrorSuggestsReplacingKey = false;
        LastRefreshed = null;
        _refreshToken++;
        IsRefreshing = false;
    }

    private void ReloadKey()
    {
        _apiKey = CredentialManager.Load();
        HasKey = _apiKey is not null;
        State = HasKey ? BalanceState.Loading : BalanceState.NoKey;
        if (HasKey)
        {
            Refresh();
        }
    }

    private void StartRefresh(bool force)
    {
        if (!HasKey || _apiKey is null)
        {
            State = BalanceState.NoKey;
            return;
        }

        if (!force && IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        if (State != BalanceState.Loaded)
        {
            State = BalanceState.Loading;
        }

        _refreshToken++;
        _ = PerformRefreshAsync(_refreshToken);
    }

    private async Task PerformRefreshAsync(int token)
    {
        var key = _apiKey;
        if (key is null)
        {
            if (token == _refreshToken)
            {
                IsRefreshing = false;
                State = BalanceState.NoKey;
            }

            return;
        }

        DeepSeekBalance? balance = null;
        BalanceException? failure = null;
        try
        {
            balance = await DeepSeekBalanceClient.FetchAsync(key);
        }
        catch (BalanceException exception)
        {
            failure = exception;
        }
        catch (Exception exception)
        {
            failure = new BalanceException(BalanceErrorKind.Network, exception.Message);
        }

        if (token != _refreshToken)
        {
            return;
        }

        IsRefreshing = false;
        if (balance is not null)
        {
            Balance = balance;
            ErrorMessage = null;
            ErrorSuggestsReplacingKey = false;
            State = BalanceState.Loaded;
            LastRefreshed = DateTimeOffset.UtcNow;
        }
        else
        {
            ErrorMessage = failure?.Describe() ?? Strings.Get("balance.error.decoding");
            ErrorSuggestsReplacingKey = failure?.SuggestsReplacingKey ?? false;
            State = BalanceState.Failed;
        }
    }

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
