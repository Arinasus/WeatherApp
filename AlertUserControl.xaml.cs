using MySqlX.XDevAPI.Common;
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
using WeatherApp.Pages;

namespace WeatherApp
{
    /// <summary>
    /// Логика взаимодействия для AlertUserControl.xaml
    /// </summary>
    public partial class AlertUserControl
    {
        DBWorking db = new DBWorking();
        UserAlert ua = new UserAlert();
        //private ObservableCollection<NewsItem> NewsItems = new ObservableCollection<NewsItem>();
        public AlertUserControl()
        {
            InitializeComponent();
        }
        private void PublishButton_Click(object sender, RoutedEventArgs e)
        {
            db.LoadDataForAlert();
            string content = AlertTextBox.Text.Trim();
            string typeText = (TypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string priorityText = PriorityPanel.Children
                .OfType<RadioButton>()
                .FirstOrDefault(rb => rb.IsChecked == true)?.Content.ToString();

            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(typeText) || string.IsNullOrWhiteSpace(priorityText))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.");
                return;
            }

            // Преобразование строки в enum
            NewsType type;
            switch (typeText)
            {
                case "Погодное предупреждение":
                    type = NewsType.Weather;
                    break;
                case "Технические работы":
                    type = NewsType.Warning;
                    break;
                case "Важная информация":
                    type = NewsType.Event;
                    break;
                case "Рекомендация":
                    type = NewsType.Recommendation;
                    break;
                default:
                    type = NewsType.News;
                    break;
            }

                NewsPriority priority;
            switch (priorityText)
            {
                case "Низкий":
                    priority = NewsPriority.Low;
                    break;
                case "Средний":
                    priority = NewsPriority.Normal;
                    break;
                case "Высокий":
                    priority = NewsPriority.High;
                    break;
                default:
                    priority = NewsPriority.Normal;
                    break;
            }

            var news = new NewsItem
            {
                Title = typeText,
                Content = content,
                PublishDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                Type = type,
                Priority = priority
            };

            db.InsertNewsItem(news);
            AddNewsItem(news); // Добавим в ObservableCollection, чтобы отобразилось в UI
            //AlertTextBox.Clear(); // Очистим поле после публикации
        }
        public void AddNewsItem(NewsItem item)
        {
            ua.NewsItems.Insert(0, item); // Добавляем в начало списка
        }
    }
}
