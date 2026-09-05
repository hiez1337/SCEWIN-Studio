using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace SCEWIN_Studio.Collections;

/// <summary>
/// High-performance ObservableCollection implementation supporting bulk range operations
/// (ReplaceRange, AddRange) that suppress per-item notifications and raise a single Reset
/// notification along with Count and Item[] property change notifications.
/// Fully thread-safe and Dispatcher-aware.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    public ObservableRangeCollection() : base() { }

    public ObservableRangeCollection(IEnumerable<T> collection) : base(collection) { }

    public ObservableRangeCollection(List<T> list) : base(list) { }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnPropertyChanged(e);
        }
    }

    /// <summary>
    /// Replaces all elements in the collection with the specified collection.
    /// Clears existing items and adds new items while suppressing individual change events,
    /// then raises a single NotifyCollectionChangedAction.Reset event along with Count and Item[] PropertyChanged events.
    /// Thread-safe: dispatches to UI thread if called from a background thread.
    /// </summary>
    /// <param name="collection">The collection whose elements should replace the current items.</param>
    public void ReplaceRange(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ReplaceRange(collection));
            return;
        }

        if (ReferenceEquals(collection, this))
        {
            return;
        }

        var itemsToAdd = collection as IReadOnlyCollection<T> ?? collection.ToList();

        _suppressNotification = true;
        try
        {
            Items.Clear();
            if (Items is List<T> list)
            {
                list.Capacity = Math.Max(list.Capacity, itemsToAdd.Count);
            }

            foreach (var item in itemsToAdd)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
        }

        using (BlockReentrancy())
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    /// <summary>
    /// Adds a range of elements to the collection while suppressing individual change events,
    /// then raises a single NotifyCollectionChangedAction.Reset event along with Count and Item[] PropertyChanged events.
    /// Thread-safe: dispatches to UI thread if called from a background thread.
    /// </summary>
    /// <param name="collection">The collection of elements to add.</param>
    public void AddRange(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => AddRange(collection));
            return;
        }

        var itemsToAdd = collection as IReadOnlyCollection<T> ?? collection.ToList();
        if (itemsToAdd.Count == 0)
        {
            return;
        }

        _suppressNotification = true;
        try
        {
            if (Items is List<T> list)
            {
                list.Capacity = Math.Max(list.Capacity, list.Count + itemsToAdd.Count);
            }

            foreach (var item in itemsToAdd)
            {
                Items.Add(item);
            }
        }
        finally
        {
            _suppressNotification = false;
        }

        using (BlockReentrancy())
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
