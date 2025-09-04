using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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

namespace WeatherApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для UserAlert.xaml
    /// </summary>
    public partial class UserAlert : Page
    {
        DBWorking db = new DBWorking();
        public ObservableCollection<NewsItem> NewsItems { get; set; }
        public UserAlert()
        {
            InitializeComponent();
            NewsItems = new ObservableCollection<NewsItem>();
            DataContext = this;
        }

        private void Page_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            LoadNewsItems();
        }
        private void LoadNewsItems()
        {
            try
            {
                db.LoadDataForAlert();
                NewsItems.Clear();
            var connection = new MySqlConnection(connectionString);
            connection.Open();
            var command = new MySqlCommand("SELECT * FROM News", connection);
                           //var command = new MySqlCommand("SELECT * FROM News ORDER BY publish_date DESC", connection);
            var reader = command.ExecuteReader();

            while (reader.Read())
            {
                NewsItems.Add(new NewsItem
                {
                    Id = reader.GetInt32("id"),
                    Title = reader.GetString("title"),
                    Content = reader.GetString("content"),
                    PublishDate = reader.GetDateTime("publish_date").ToString("dd.MM.yyyy HH:mm"),
                    Type = Enum.TryParse(reader.GetString("type"), out NewsType type) ? type : NewsType.Weather,
                    Priority = Enum.TryParse(reader.GetString("priority"), out NewsPriority priority) ? priority : NewsPriority.Normal
                });
            }
                NewsItemsControl.ItemsSource = NewsItems;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при загрузке новостей: " + ex.Message);
            }
        }

        // Метод для добавления новой новости из AlertUserControl
        public void AddNewsItem(NewsItem item)
        {
            NewsItems.Insert(0, item); // Добавляем в начало списка
        }
    }

    public class NewsItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string PublishDate { get; set; }
        public NewsPriority Priority { get; set; } = NewsPriority.Normal;
        public NewsType Type { get; set; } = NewsType.Weather;
    }
    public enum NewsPriority
    {
        Low,       // Низкая важность
        Normal,    // Средняя важность
        High,      // Высокая важность
        Critical   // Критическая важность
    }

    public enum NewsType
    {
        Weather,       // Прогноз погоды
        Warning,       // Предупреждение
        Event,         // Мероприятие
        Recommendation, // Рекомендация
        News
    }
   public class PriorityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NewsPriority priority)
            {
                Brush brush;
                switch (priority)
                {
                    case NewsPriority.Low:
                        brush = Brushes.LightGray;
                        break;
                    case NewsPriority.Normal:
                        brush = Brushes.AliceBlue;
                        break;
                    case NewsPriority.High:
                        brush = Brushes.LightGoldenrodYellow;
                        break;
                    case NewsPriority.Critical:
                        brush = Brushes.LightCoral;
                        break;
                    default:
                        brush = Brushes.White;
                        break;
                }
                return brush;
            }
            return Brushes.White;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NewsTypeToTitleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NewsType type)
            {
               string result;
            switch (type)
            {
                case NewsType.Weather:
                    result = "МЕТЕО";
                    break;
                case NewsType.Warning:
                    result = "ПРЕДУПРЕЖДЕНИЕ";
                    break;
                case NewsType.Event:
                    result = "МЕРОПРИЯТИЕ";
                    break;
                case NewsType.Recommendation:
                    result = "РЕКОМЕНДАЦИЯ";
                    break;
                default:
                    result = "НОВОСТЬ";
                    break;
            }
            return result;
            }
            return "НОВОСТЬ";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
