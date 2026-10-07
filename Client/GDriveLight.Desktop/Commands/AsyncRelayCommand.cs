using System.Windows.Input;

namespace GDriveLight.Desktop.Commands;

public class AsyncRelayCommand<T> : ICommand where T : class
{
    private readonly Func<T?, Task> _execute;
    private readonly Action<Exception> _onError;
    private readonly Predicate<T?>? _canExecute;

    public AsyncRelayCommand(Func<T?, Task> execute, Action<Exception> onError, Predicate<T?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(onError);
        _execute = execute;
        _onError = onError;
        _canExecute = canExecute;
    }

    public bool IsExecuting { get; private set; }
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !IsExecuting && (parameter is null || parameter is T) && (_canExecute?.Invoke(parameter as T) ?? true);

    // async void is restricted to the ICommand event boundary. Tests and callers can await ExecuteAsync.
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            await ExecuteAsync(parameter as T);
        }
        catch (Exception exception)
        {
            _onError(exception);
        }
    }

    public async Task ExecuteAsync(T? parameter = null)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        IsExecuting = true;
        NotifyCanExecuteChanged();
        try
        {
            await _execute(parameter);
        }
        finally
        {
            IsExecuting = false;
            NotifyCanExecuteChanged();
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncRelayCommand : AsyncRelayCommand<object>
{
    public AsyncRelayCommand(Func<Task> execute, Action<Exception> onError, Func<bool>? canExecute = null)
        : base(_ => execute(), onError, _ => canExecute?.Invoke() ?? true)
    {
        ArgumentNullException.ThrowIfNull(execute);
    }
}
