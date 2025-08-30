using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AdbInstallerApp.Behaviors
{
    public static class AutoScrollBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(AutoScrollBehavior),
                new PropertyMetadata(false, OnChanged));

        public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject d, bool v) => d.SetValue(IsEnabledProperty, v);

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListBox lb && (bool)e.NewValue)
            {
                lb.Loaded += (_, __) => Hook(lb);
            }
        }

        private static void Hook(ListBox lb)
        {
            if (lb.Items is INotifyCollectionChanged incc)
            {
                var sv = FindDescendant<ScrollViewer>(lb);
                incc.CollectionChanged += (_, args) =>
                {
                    if (sv is null) return;
                    bool isAtEnd = sv.VerticalOffset >= sv.ScrollableHeight - 2;
                    if (args.Action is NotifyCollectionChangedAction.Add && isAtEnd)
                        sv.ScrollToEnd();
                };
            }
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is T t) return t;
                var r = FindDescendant<T>(c);
                if (r != null) return r;
            }
            return null;
        }
    }
}
