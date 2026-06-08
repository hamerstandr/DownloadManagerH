using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace DownloadManager.Models
{
    /// <summary>
    /// کلاس کمکی برای بهینه‌سازی مجازی‌سازی رابط کاربری
    /// این کلاس برای بهبود عملکرد لیست‌های بزرگ دانلود استفاده می‌شود
    /// </summary>
    public class UIVirtualizationHelper
    {
        private readonly Dispatcher _dispatcher;
        private readonly Dictionary<string, VirtualizedCollection> _virtualizedCollections;
        private const int DEFAULT_PAGE_SIZE = 50;
        private const int VIEWPORT_BUFFER = 10;

        public UIVirtualizationHelper()
        {
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _virtualizedCollections = new Dictionary<string, VirtualizedCollection>();
        }

        /// <summary>
        /// ایجاد مجموعه مجازی‌سازی شده برای لیست دانلودها
        /// </summary>
        public VirtualizedCollection<T> CreateVirtualizedCollection<T>(
            IList<T> sourceCollection,
            string collectionId,
            int pageSize = DEFAULT_PAGE_SIZE)
        {
            var virtualizedCollection = new VirtualizedCollection<T>(sourceCollection, pageSize);
            _virtualizedCollections[collectionId] = virtualizedCollection;
            return virtualizedCollection;
        }

        /// <summary>
        /// بهینه‌سازی DataGrid برای مجازی‌سازی
        /// </summary>
        public void OptimizeDataGridForVirtualization(DataGrid dataGrid)
        {
            if (dataGrid == null) return;

            // فعال‌سازی مجازی‌سازی ردیف
            dataGrid.EnableRowVirtualization = true;
            dataGrid.EnableColumnVirtualization = true;

            // تنظیم حالت انتخاب برای بهبود عملکرد
            dataGrid.SelectionMode = DataGridSelectionMode.Single;
            dataGrid.SelectionUnit = DataGridSelectionUnit.FullRow;

            // بهینه‌سازی اسکرول
            ScrollViewer.SetCanContentScroll(dataGrid, true);

            // تنظیم اندازه کش
            VirtualizingPanel.SetCacheLengthUnit(dataGrid, VirtualizationCacheLengthUnit.Item);
            VirtualizingPanel.SetCacheLength(dataGrid, new VirtualizationCacheLength(20, 20));

            // بهینه‌سازی حالت مجازی‌سازی
            VirtualizingPanel.SetVirtualizationMode(dataGrid, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(dataGrid, ScrollUnit.Item);
        }

        /// <summary>
        /// بهینه‌سازی ListView برای مجازی‌سازی
        /// </summary>
        public void OptimizeListViewForVirtualization(ListView listView)
        {
            if (listView == null) return;

            // تنظیم پنل مجازی‌سازی
            var virtualizingStackPanel = new VirtualizingStackPanel();
            VirtualizingPanel.SetVirtualizationMode(virtualizingStackPanel, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(virtualizingStackPanel, ScrollUnit.Item);

            listView.ItemsPanel = new ItemsPanelTemplate();
            listView.ItemsPanel.VisualTree = new FrameworkElementFactory(typeof(VirtualizingStackPanel));

            // بهینه‌سازی اسکرول
            ScrollViewer.SetCanContentScroll(listView, true);
            
            // تنظیم کش
            VirtualizingPanel.SetCacheLengthUnit(listView, VirtualizationCacheLengthUnit.Item);
            VirtualizingPanel.SetCacheLength(listView, new VirtualizationCacheLength(15, 15));
        }

        /// <summary>
        /// پاک‌سازی منابع مجازی‌سازی
        /// </summary>
        public void CleanupVirtualization(string collectionId)
        {
            if (_virtualizedCollections.ContainsKey(collectionId))
            {
                _virtualizedCollections[collectionId].Dispose();
                _virtualizedCollections.Remove(collectionId);
            }
        }

        /// <summary>
        /// پاک‌سازی تمام منابع
        /// </summary>
        public void CleanupAll()
        {
            foreach (var collection in _virtualizedCollections.Values)
            {
                collection.Dispose();
            }
            _virtualizedCollections.Clear();
        }

        /// <summary>
        /// بررسی وضعیت حافظه و بهینه‌سازی در صورت نیاز
        /// </summary>
        public void OptimizeMemoryUsage()
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                GC.Collect(0, GCCollectionMode.Optimized);
                
                foreach (var collection in _virtualizedCollections.Values)
                {
                    collection.OptimizeMemory();
                }
            }), DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// کلاس پایه برای مجموعه‌های مجازی‌سازی شده
    /// </summary>
    public abstract class VirtualizedCollection : IDisposable
    {
        protected bool _disposed = false;

        public abstract void OptimizeMemory();
        
        public virtual void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }

    /// <summary>
    /// مجموعه مجازی‌سازی شده برای انواع مختلف داده
    /// </summary>
    public class VirtualizedCollection<T> : VirtualizedCollection, IList<T>, INotifyPropertyChanged
    {
        private readonly IList<T> _sourceCollection;
        private readonly int _pageSize;
        private readonly Dictionary<int, T> _cache;
        private readonly object _lockObject = new object();
        private new bool _disposed = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public VirtualizedCollection(IList<T> sourceCollection, int pageSize = 50)
        {
            _sourceCollection = sourceCollection ?? throw new ArgumentNullException(nameof(sourceCollection));
            _pageSize = pageSize;
            _cache = new Dictionary<int, T>();
        }

        public int Count => _sourceCollection.Count;
        public bool IsReadOnly => _sourceCollection.IsReadOnly;

        public T this[int index]
        {
            get
            {
                lock (_lockObject)
                {
                    if (_cache.ContainsKey(index))
                        return _cache[index];

                    if (index >= 0 && index < _sourceCollection.Count)
                    {
                        var item = _sourceCollection[index];
                        _cache[index] = item;
                        
                        // محدود کردن اندازه کش
                        if (_cache.Count > _pageSize * 2)
                        {
                            var keysToRemove = _cache.Keys.OrderBy(k => Math.Abs(k - index)).Skip(_pageSize).ToList();
                            foreach (var key in keysToRemove)
                            {
                                _cache.Remove(key);
                            }
                        }
                        
                        return item;
                    }
                    
                    return default(T);
                }
            }
            set
            {
                if (index >= 0 && index < _sourceCollection.Count)
                {
                    _sourceCollection[index] = value;
                    lock (_lockObject)
                    {
                        _cache[index] = value;
                    }
                    OnPropertyChanged($"Item[{index}]");
                }
            }
        }

        public void Add(T item)
        {
            _sourceCollection.Add(item);
            OnPropertyChanged(nameof(Count));
        }

        public void Clear()
        {
            _sourceCollection.Clear();
            lock (_lockObject)
            {
                _cache.Clear();
            }
            OnPropertyChanged(nameof(Count));
        }

        public bool Contains(T item) => _sourceCollection.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => _sourceCollection.CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator() => _sourceCollection.GetEnumerator();

        public int IndexOf(T item) => _sourceCollection.IndexOf(item);

        public void Insert(int index, T item)
        {
            _sourceCollection.Insert(index, item);
            lock (_lockObject)
            {
                _cache.Clear(); // پاک کردن کش پس از تغییر ایندکس‌ها
            }
            OnPropertyChanged(nameof(Count));
        }

        public bool Remove(T item)
        {
            var result = _sourceCollection.Remove(item);
            if (result)
            {
                lock (_lockObject)
                {
                    _cache.Clear(); // پاک کردن کش پس از تغییر ایندکس‌ها
                }
                OnPropertyChanged(nameof(Count));
            }
            return result;
        }

        public void RemoveAt(int index)
        {
            _sourceCollection.RemoveAt(index);
            lock (_lockObject)
            {
                _cache.Clear(); // پاک کردن کش پس از تغییر ایندکس‌ها
            }
            OnPropertyChanged(nameof(Count));
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public override void OptimizeMemory()
        {
            lock (_lockObject)
            {
                if (_cache.Count > _pageSize)
                {
                    var keysToKeep = _cache.Keys.Take(_pageSize).ToList();
                    var newCache = new Dictionary<int, T>();
                    
                    foreach (var key in keysToKeep)
                    {
                        newCache[key] = _cache[key];
                    }
                    
                    _cache.Clear();
                    foreach (var kvp in newCache)
                    {
                        _cache[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                lock (_lockObject)
                {
                    _cache.Clear();
                }
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// کلاس کمکی برای مدیریت عملکرد اسکرول
    /// </summary>
    public static class ScrollOptimizer
    {
        /// <summary>
        /// بهینه‌سازی اسکرول برای کنترل‌های لیست
        /// </summary>
        public static void OptimizeScrolling(ItemsControl itemsControl)
        {
            if (itemsControl == null) return;

            // تنظیم مجازی‌سازی
            VirtualizingPanel.SetVirtualizationMode(itemsControl, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(itemsControl, ScrollUnit.Item);
            
            // بهینه‌سازی کش
            VirtualizingPanel.SetCacheLengthUnit(itemsControl, VirtualizationCacheLengthUnit.Item);
            VirtualizingPanel.SetCacheLength(itemsControl, new VirtualizationCacheLength(10, 10));

            // تنظیم اسکرول
            if (itemsControl.Template?.FindName("PART_ScrollViewer", itemsControl) is ScrollViewer scrollViewer)
            {
                scrollViewer.CanContentScroll = true;
                ScrollViewer.SetPanningMode(scrollViewer, PanningMode.VerticalOnly);
            }
        }

        /// <summary>
        /// اسکرول نرم به آیتم مشخص
        /// </summary>
        public static void SmoothScrollToItem(ItemsControl itemsControl, object item)
        {
            if (itemsControl?.Items == null || item == null) return;

            var index = itemsControl.Items.IndexOf(item);
            if (index >= 0)
            {
                SmoothScrollToIndex(itemsControl, index);
            }
        }

        /// <summary>
        /// اسکرول نرم به ایندکس مشخص
        /// </summary>
        public static void SmoothScrollToIndex(ItemsControl itemsControl, int index)
        {
            if (itemsControl?.Template?.FindName("PART_ScrollViewer", itemsControl) is ScrollViewer scrollViewer)
            {
                var itemHeight = GetEstimatedItemHeight(itemsControl);
                var targetOffset = index * itemHeight;
                
                AnimateScrollToOffset(scrollViewer, targetOffset);
            }
        }

        private static double GetEstimatedItemHeight(ItemsControl itemsControl)
        {
            if (itemsControl.Items.Count > 0)
            {
                var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
                return container?.ActualHeight ?? 25.0; // مقدار پیش‌فرض
            }
            return 25.0;
        }

        private static void AnimateScrollToOffset(ScrollViewer scrollViewer, double targetOffset)
        {
            var currentOffset = scrollViewer.VerticalOffset;
            var distance = targetOffset - currentOffset;
            var duration = TimeSpan.FromMilliseconds(Math.Min(500, Math.Abs(distance) * 2));

            var animation = new System.Windows.Media.Animation.DoubleAnimation(
                currentOffset, targetOffset, duration)
            {
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase()
            };

            scrollViewer.BeginAnimation(ScrollViewer.VerticalOffsetProperty, animation);
        }
    }
}