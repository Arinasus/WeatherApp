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

namespace WeatherApp
{
    /// <summary>
    /// Логика взаимодействия для EventsUserControl.xaml
    /// </summary>
    public partial class EventsUserControl : UserControl
    {
        private readonly DBWorking db;
        private ObservableCollection<Event> events;
        public EventsUserControl()
        {
            InitializeComponent();
            db = new DBWorking();
            db.InitializeEventsTable();
            LoadEvents();
        }
        private void LoadEvents()
        {
            events = new ObservableCollection<Event>(db.GetAllEvents());
            EventsListView.ItemsSource = events;
        }
        private void AddEventButton_Click(object sender, RoutedEventArgs e)
        {
            // Получаем данные из полей ввода
            string name = EventNameTextBox.Text;
            DateTime date = EventDatePicker.SelectedDate ?? DateTime.Now;
            string description = DescriptionTextBox.Text;

            // Проверка заполнения обязательных полей
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введите название мероприятия");
                return;
            }

            // Добавляем мероприятие в БД
            if (db.AddEvent(name, date, description))
            {
                // Обновляем список мероприятий
                LoadEvents();

                // Очищаем поля ввода
                EventNameTextBox.Text = string.Empty;
                EventDatePicker.SelectedDate = null;
                DescriptionTextBox.Text = string.Empty;

                MessageBox.Show("Мероприятие успешно добавлено");
            }
            else
            {
                MessageBox.Show("Не удалось добавить мероприятие");
            }
        }

        private void DeleteEventButton_Click(object sender, RoutedEventArgs e)
        {
            if (EventsListView.SelectedItem is Event selectedEvent)
            {
                DeleteEvent(selectedEvent.Id);
            }
            else
            {
                MessageBox.Show("Выберите мероприятие для удаления");
            }
        }
        private void DeleteEvent(int eventId)
        {
            if (MessageBox.Show("Вы уверены, что хотите удалить это мероприятие?",
                              "Подтверждение удаления",
                              MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (db.DeleteEvent(eventId))
                {
                    LoadEvents(); // Обновляем список после удаления
                    MessageBox.Show("Мероприятие успешно удалено");
                }
                else
                {
                    MessageBox.Show("Не удалось удалить мероприятие");
                }
            }
        }
    }
}
