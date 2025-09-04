using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static WeatherApp.DBWorking;

namespace WeatherApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для Measure.xaml
    /// </summary>
    public partial class Measure : UserControl
    {
        private DBWorking db;
        public ObservableCollection<Event> Events { get; set; } = new ObservableCollection<Event>();
        //public ObservableCollection<Event> Events { get; set; }
        public Measure()
        {
            InitializeComponent();
            DataContext = this;
            db = new DBWorking();
            LoadEvents();
            Loaded += (s, e) => UpdateJoinButtons();
            SessionManager.OnLogout += () =>
            {
                // Обновляем кнопки при логауте
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateJoinButtons();
                });
            };
        }
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                var childResult = FindVisualChild<T>(child);
                if (childResult != null)
                    return childResult;
            }
            return null;
        }
        private void LoadEvents()
        {
            var eventsList = db.GetAllEvents();
            Events.Clear();
            foreach (var ev in eventsList)
                Events.Add(ev);
            /*var eventsList = db.GetAllEvents();
            Events = new ObservableCollection<Event>(eventsList);*/
        }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        private void JoinButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var button = sender as Button;
            var ev = button?.Tag as Event;

            if (ev == null) return;

            // Если не авторизован
            if (string.IsNullOrEmpty(SessionManager.CurrentUserEmail))
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                var registerWindow = new RegisterWindow(mainWindow);
                if (registerWindow.ShowDialog() == true)
                {
                    // Регистрация успешна
                    MessageBox.Show("Регистрация прошла успешно!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Пользователь закрыл окно — не регистрируем
                    return;
                }
            }

            // Если уже участвует
            if (SessionManager.JoinedEventIds.Contains(ev.Id))
            {
                MessageBox.Show("Вы уже участвуете в этом мероприятии.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool success = db.AddParticipation(SessionManager.CurrentUserEmail, ev.Id);
            if (success)
            {
                SessionManager.JoinedEventIds.Add(ev.Id); // локально отмечаем
                MessageBox.Show($"Вы успешно записались на мероприятие: {ev.Name}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateJoinButtons();
            }
            else
            {
                MessageBox.Show("Вы уже зарегистрированы на это мероприятие.", "Информация", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void UpdateJoinButtons()
        {
            foreach (var child in FindVisualChildren<Button>(this))
            {
                if (child.Tag is Event ev)
                {
                    if (SessionManager.JoinedEventIds.Contains(ev.Id))
                    {
                        child.Content = "Вы участвуете";
                        child.IsEnabled = false;
                        child.Background = new SolidColorBrush(Colors.Gray);
                    }
                    else
                    {
                        // Обратно активируем кнопку и текст, если нужно
                        child.Content = "Принять участие";
                        child.IsEnabled = true;
                        child.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#2B579A"));
                    }
                }
            }
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T obj)
                {
                    yield return obj;
                }

                foreach (T childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }
    }
}
