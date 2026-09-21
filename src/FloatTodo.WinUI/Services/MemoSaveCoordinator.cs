using FloatTodo.Core.Services;
using FloatTodo.ViewModels;
using Microsoft.UI.Dispatching;
namespace FloatTodo.WinUI.Services;

public sealed class MemoSaveCoordinator
{
    private readonly MemoService _storage;
    private readonly TodayViewModel _viewModel;
    private readonly DispatcherQueueTimer _timer;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private long _revision, _saved;
    public MemoSaveCoordinator(MemoService storage, TodayViewModel viewModel, DispatcherQueue dispatcher)
    {
        _storage = storage; _viewModel = viewModel;
        _timer = dispatcher.CreateTimer(); _timer.Interval = TimeSpan.FromMilliseconds(450); _timer.IsRepeating = false;
        _timer.Tick += async (_, _) => await FlushAsync();
        viewModel.SaveRequested += () => { _revision++; _timer.Stop(); _timer.Start(); };
    }
    public async Task<bool> FlushAsync()
    {
        _timer.Stop();
        await _writeGate.WaitAsync();
        try
        {
            if (_saved == _revision) return true;
            var revision = _revision;
            var snapshot = _viewModel.Snapshot();
            await Task.Run(() => _storage.Save(snapshot));
            _saved = revision; _viewModel.ErrorMessage = "";
            if (_saved != _revision) _timer.Start();
            return true;
        }
        catch (Exception e) { _viewModel.ErrorMessage = Shell.Loc.Get("SaveFailed") + e.Message; return false; }
        finally { _writeGate.Release(); }
    }

    public void FlushSync()
    {
        _timer.Stop();
        if (_writeGate.Wait(TimeSpan.FromMilliseconds(500)))
        {
            try
            {
                if (_saved == _revision) return;
                var revision = _revision;
                var snapshot = _viewModel.Snapshot();
                _storage.Save(snapshot);
                _saved = revision;
                _viewModel.ErrorMessage = "";
            }
            catch (Exception e)
            {
                _viewModel.ErrorMessage = Shell.Loc.Get("SaveFailed") + e.Message;
            }
            finally
            {
                _writeGate.Release();
            }
        }
    }
}
