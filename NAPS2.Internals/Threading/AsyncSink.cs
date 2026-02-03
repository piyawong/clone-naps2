// ReSharper disable once CheckNamespace
namespace NAPS2.Util;

public class AsyncSink<T> where T : class
{
    private static TaskCompletionSource<T?> CreateTcs() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly List<TaskCompletionSource<T?>> _items = [CreateTcs()];
    private bool _completed;

    public async IAsyncEnumerable<T> AsAsyncEnumerable()
    {
        int i = 0;
        Console.Error.WriteLine($"🔄 [AsyncSink] AsAsyncEnumerable started");
        while (true)
        {
            TaskCompletionSource<T?> tcs;
            lock (this)
            {
                tcs = _items[i++];
                Console.Error.WriteLine($"🔄 [AsyncSink] Consumer reading item #{i}, total items in list: {_items.Count}, completed: {_completed}");
            }
            var item = await tcs.Task;
            if (item == null)
            {
                Console.Error.WriteLine($"🔄 [AsyncSink] Received sentinel (null) at item #{i} - ending enumeration");
                yield break;
            }
            Console.Error.WriteLine($"🔄 [AsyncSink] Yielding item #{i}");
            yield return item;
        }
    }

    public void SetCompleted()
    {
        lock (this)
        {
            if (_completed)
            {
                Console.Error.WriteLine($"🔄 [AsyncSink] SetCompleted called but already completed");
                return;
            }
            _completed = true;
            _items.Last().SetResult(null);
            Console.Error.WriteLine($"🔄 [AsyncSink] SetCompleted - sent sentinel (null) to item #{_items.Count}");
        }
    }

    public void SetError(Exception ex)
    {
        if (ex == null)
        {
            throw new ArgumentNullException(nameof(ex));
        }
        lock (this)
        {
            if (_completed)
            {
                throw new InvalidOperationException("Sink is already in the completed state");
            }
            _completed = true;
            ex.PreserveStackTrace();
            _items.Last().SetException(ex);
        }
    }

    public void PutItem(T item)
    {
        lock (this)
        {
            _items.Last().SetResult(item);
            _items.Add(CreateTcs());
            Console.Error.WriteLine($"🔄 [AsyncSink] PutItem - added item #{_items.Count - 1}, new pending item created");
        }
    }
}