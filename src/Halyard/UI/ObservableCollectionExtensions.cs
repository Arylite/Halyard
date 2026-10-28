using System.Collections.ObjectModel;

namespace Halyard.UI;

public static class ObservableCollectionExtensions
{
    /// <summary>
    /// Makes <paramref name="target"/> equal to <paramref name="source"/> with the fewest removals,
    /// moves and inserts, so bound lists keep their containers instead of rebuilding everything.
    /// </summary>
    public static void SyncTo<T>(this ObservableCollection<T> target, IReadOnlyList<T> source)
        where T : class
    {
        var wanted = source.ToHashSet(ReferenceEqualityComparer.Instance);
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }

        for (var i = 0; i < source.Count; i++)
        {
            if (i < target.Count && ReferenceEquals(target[i], source[i]))
            {
                continue;
            }

            var current = target.IndexOf(source[i]);
            if (current >= 0)
            {
                target.Move(current, i);
            }
            else
            {
                target.Insert(i, source[i]);
            }
        }
    }
}
