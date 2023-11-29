using System.Collections;
using System.Collections.Concurrent;

namespace DevPack4Dataverse.Utils;

internal class ThreadSafeBagTakeMany<T> : IEnumerable<T>
{
    private readonly ConcurrentBag<T> _bag = [];

    public void Add(T item)
    {
        _bag.Add(item);
    }

    public int Count => _bag.Count;

    public void AddRange(IEnumerable<T> items)
    {
        foreach (T item in items)
        {
            _bag.Add(item);
        }
    }

    public IReadOnlyList<T> TakeMany(int count)
    {
        List<T> items = [];

        for (int i = 0; i < count; i++)
        {
            if (_bag.TryTake(out T item))
            {
                items.Add(item);
            }
            else
            {
                break;
            }
        }

        return items;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _bag.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _bag.GetEnumerator();
    }
}
