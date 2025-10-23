using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

public static class Utils
{
    public static bool TryGetDirectory(string path, [NotNullWhen(true)] out DirectoryInfo? directoryInfo) {
        try {
            directoryInfo = new DirectoryInfo(path);
            return true;
        } catch (ArgumentException) {
            directoryInfo = null;
            return false;
        }
    }

    public static void AddRange<T>(this Collection<T> coll, IEnumerable<T> values) {
        foreach (var val in values) coll.Add(val);
    }

    public static void AddRange<TKey, TValue>(
        this Dictionary<TKey, TValue> coll,
        Dictionary<TKey, TValue> values
    ) where TKey : notnull
    {
        foreach (var (key, val) in values) coll.Add(key, val);
    }
}