using Microsoft.Xaml.Behaviors;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Convert.UI.Behaviors
{
    public class DataGridAutoScrollBehavior : Behavior<DataGrid>
    {
        private bool _isLoaded = false;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += (s, e) => _isLoaded = true;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _isLoaded = true;
            HookCollectionChanged();
            ScrollToEnd();
        }

        private void HookCollectionChanged()
        {
            if (AssociatedObject.ItemsSource is INotifyCollectionChanged incc)
            {
                incc.CollectionChanged += OnCollectionChanged;

                // S'abonner aux items déjà présents
                foreach (var item in AssociatedObject.Items)
                {
                    if (item is INotifyPropertyChanged npc)
                        npc.PropertyChanged += OnItemPropertyChanged;
                }
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is INotifyPropertyChanged npc)
                        npc.PropertyChanged += OnItemPropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is INotifyPropertyChanged npc)
                        npc.PropertyChanged -= OnItemPropertyChanged;
                }
            }

            ScrollToEnd();
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // On ne scroll que sur les propriétés qui t'intéressent
            if (e.PropertyName == "Progress" || e.PropertyName == "Status")
            {
                ScrollToItem(sender);
            }
        }

        private void ScrollToItem(object item)
        {
            if (!_isLoaded)
                return;

            if (item == null)
                return;

            if (AssociatedObject.Items.Count == 0)
                return;

            Application.Current.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    try
                    {
                        AssociatedObject.UpdateLayout();
                        AssociatedObject.ScrollIntoView(item);
                    }
                    catch
                    {
                        // Ignore proprement
                    }
                }),
                System.Windows.Threading.DispatcherPriority.Background
            );
        }

        private void ScrollToEnd()
        {
            if (!_isLoaded)
                return;

            if (AssociatedObject.Items.Count == 0)
                return;

            Application.Current.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    try
                    {
                        AssociatedObject.UpdateLayout();
                        var last = AssociatedObject.Items[AssociatedObject.Items.Count - 1];
                        AssociatedObject.ScrollIntoView(last);
                    }
                    catch { }
                }),
                System.Windows.Threading.DispatcherPriority.Background
            );
        }
    }
}
