﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using HeightMapExtractorGUI.ViewModels;


namespace HeightMapExtractorGUI.Models
{
    // SimpleMVVM/ Visual Studio SDK
    public partial class BulkObservableCollection<T> : Collection<T>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private int bulkOperationCount;
        private bool collectionChangedDuringBulkOperation;


        public int EnsureCapacity(int capacity)
        {
            if (Items is not List<T>)
            {
                throw new InvalidOperationException("EnsureCapacity can only be called on collections with underlying List<T>");
            }

            return ((List<T>)Items).EnsureCapacity(capacity);
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            var handler = CollectionChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, PropertyChangedEventArgsCache.GetOrCreate(name));
            }
        }

        private void OnCollectionChanged(NotifyCollectionChangedAction action, T item, int index)
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, item, index));
        }

        private void OnCollectionChanged(NotifyCollectionChangedAction action, T item, T oldItem, int index)
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, item, oldItem, index));
        }

        private void OnCollectionReset()
        {
            OnCountChanged();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void OnCountChanged()
        {
            OnPropertyChanged("Count");
            OnItemsChanged();
        }

        private void OnItemsChanged()
        {
            OnPropertyChanged("Items[]");
        }

        protected override void ClearItems()
        {
            var hadItems = Count != 0;

            base.ClearItems();

            if (hadItems)
            {
                if (bulkOperationCount == 0)
                {
                    OnCollectionReset();
                }
                else
                {
                    collectionChangedDuringBulkOperation = true;
                }
            }
        }

        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(index, item);
            // _hashSet.Add(item.GetHashCode());
            if (bulkOperationCount == 0)
            {
                OnCountChanged();
                OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
            }
            else
            {
                collectionChangedDuringBulkOperation = true;
            }
        }
        
        public void AddSuppressed(T item)
        {
            bulkOperationCount++;
            InsertItem(Items.Count, item);
            bulkOperationCount--;
        }
        
        public void AddSuppressed(T item, int index)
        {
            bulkOperationCount++;
            InsertItem(index, item);
            bulkOperationCount--;
        }

        protected override void RemoveItem(int index)
        {
            var item = this[index];
            base.RemoveItem(index);

            if (bulkOperationCount == 0)
            {
                OnCountChanged();
                OnCollectionChanged(NotifyCollectionChangedAction.Remove, item, index);
            }
            else
            {
                collectionChangedDuringBulkOperation = true;
            }
        }

        protected override void SetItem(int index, T item)
        {
            var oldItem = this[index];
            base.SetItem(index, item);

            if (bulkOperationCount == 0)
            {
                OnItemsChanged();
                OnCollectionChanged(NotifyCollectionChangedAction.Replace, item, oldItem, index);
            }
            else
            {
                collectionChangedDuringBulkOperation = true;
            }
        }

        public void BeginBulkOperation()
        {
            bulkOperationCount++;
        }

        public void EndBulkOperation()
        {
            if (bulkOperationCount == 0)
            {
                throw new InvalidOperationException("EndBulkOperation() called without matching call to BeginBulkOperation()");
            }

            bulkOperationCount--;

            if (bulkOperationCount == 0 && collectionChangedDuringBulkOperation)
            {
                OnCollectionReset();
                collectionChangedDuringBulkOperation = false;
            }
        }

        public T Find(Func<T, bool> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            foreach (var item in this)
            {
                if (predicate(item))
                {
                    return item;
                }
            }
            
            throw new InvalidOperationException("No item found.");
        }

        public bool TryFind(Func<T, bool> predicate, out T result)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException("predicate");
            }
            
            foreach (var item in this.Items)
            {
                if (predicate(item))
                {
                    result = item;
                    return true;
                }
            }

            result = default(T);
            return false;
        }

        public bool TryFind(T item, out T result)
        {
            // if (!_hashSet.Contains(item.GetHashCode()))
            // {
            //     result = default(T);
            //     return false;
            // }
            
            var index = IndexOf(item);
            if (index != -1)
            {
                result = this[index];
                return true;
            }

            result = default(T);
            return false;
        }
        
        public int BinarySearch(int index, int length, T value, IComparer<T> comparer)
        {
            if (comparer == null)
            {
                comparer = Comparer<T>.Default;
            }

            var low = index;
            var high = (index + length) - 1;

            while (low <= high)
            {
                var mid = low + ((high - low) / 2);
                var comp = comparer.Compare(this[mid], value);

                if (comp == 0)
                {
                    return mid;
                }

                if (comp < 0)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return ~low;
        }

        public int BinarySearch(int index, int length, T value, Comparison<T> comparison)
        {
            return comparison == null
                ? BinarySearch(index, length, value, Comparer<T>.Default)
                : BinarySearch(index, length, value, new ComparisonComparer(comparison));
        }

        public int BinarySearch(int index, int length, T value, Func<T, T, int> comparison)
        {
            return comparison == null
                ? BinarySearch(index, length, value, Comparer<T>.Default)
                : BinarySearch(index, length, value, new FuncComparer(comparison));
        }

        public int BinarySearch(int index, int length, Func<T, int> comparison)
        {
            if (comparison == null)
            {
                throw new ArgumentNullException("comparison");
            }

            var low = index;
            var high = (index + length) - 1;

            while (low <= high)
            {
                var mid = low + ((high - low) / 2);
                var comp = comparison(this[mid]);

                if (comp == 0)
                {
                    return mid;
                }

                if (comp < 0)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return ~low;
        }

        public int BinarySearch(T value, IComparer<T> comparer)
        {
            return BinarySearch(0, Count, value, comparer);
        }

        public int BinarySearch(T value, Comparison<T> comparison)
        {
            return comparison == null
                ? BinarySearch(0, Count, value, Comparer<T>.Default)
                : BinarySearch(0, Count, value, new ComparisonComparer(comparison));
        }

        public int BinarySearch(T value, Func<T, T, int> comparison)
        {
            return comparison == null
                ? BinarySearch(0, Count, value, Comparer<T>.Default)
                : BinarySearch(0, Count, value, new FuncComparer(comparison));
        }

        public int BinarySearch(Func<T, int> comparison)
        {
            return BinarySearch(0, Count, comparison);
        }

        public int BinarySearch(T value)
        {
            return BinarySearch(0, Count, value, Comparer<T>.Default);
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
            {
                return;
            }

            BeginBulkOperation();
            try
            {
                if (items is IList<T> list)
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        Add(list[i]);
                    }
                }
                else
                {
                    {
                        foreach (var item in items)
                        {
                            Add(item);
                        }
                    }
                }
            }
            finally
            {
                EndBulkOperation();
            }
        }
        
        // sort
        public void Sort(IComparer<T> comparer)
        {
            if (comparer == null)
            {
                comparer = Comparer<T>.Default;
            }

            if (Count > 0)
            {
                List<T> list = Items as List<T>;
                if (list != null)
                {
                    list.Sort(comparer);
                }
                else
                {
                    var array = new T[Count];
                    CopyTo(array, 0);
                    Array.Sort(array, comparer);
                    for (var i = 0; i < Count; i++)
                    {
                        SetItem(i, array[i]);
                    }
                }
            }
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;
    }
    
    public partial class BulkObservableCollection<T>
    {
        private class FuncComparer : IComparer<T>
        {
            private readonly Func<T, T, int> comparison;

            public FuncComparer(Func<T, T, int> comparison)
            {
                this.comparison = comparison;
            }

            public int Compare(T x, T y)
            {
                return comparison(x, y);
            }
        }
    }

    public partial class BulkObservableCollection<T>
    {
        private class ComparisonComparer : IComparer<T>
        {
            private readonly Comparison<T> comparison;

            public ComparisonComparer(Comparison<T> comparison)
            {
                this.comparison = comparison;
            }

            public int Compare(T x, T y)
            {
                return comparison(x, y);
            }
        }
    }
    
    internal static class PropertyChangedEventArgsCache
    {
        private static readonly Dictionary<string, PropertyChangedEventArgs> eventArgsCache =
            new Dictionary<string, PropertyChangedEventArgs>();

        private static readonly object gate = new object();

        public static PropertyChangedEventArgs GetOrCreate(string name)
        {
            PropertyChangedEventArgs eventArgs;

            lock (gate)
            {
                if (!eventArgsCache.TryGetValue(name, out eventArgs))
                {
                    eventArgs = new PropertyChangedEventArgs(name);
                    eventArgsCache.Add(name, eventArgs);
                }
            }

            return eventArgs;
        }
    }

    internal static class BulkObservableCollectionExtensions
    {
        public static bool TryFind(this BulkObservableCollection<MyTreeViewItem> items, string content, out MyTreeViewItem result)
        {
            foreach (var item in items)
            {
                if (item.Content == content)
                {
                    result = item;
                    return true;
                }
            }

            result = null;
            return false;
        }
    }
}