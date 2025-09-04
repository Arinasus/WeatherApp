using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UI.Controls;
using Esri.ArcGISRuntime.Tasks.Geocoding;
using System.Runtime.Remoting.Messaging;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Xml.Linq;
using Esri.ArcGISRuntime.Location;
using System.Threading;
using System.Net.Security;
using System.Net;
using Esri.ArcGISRuntime;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Drawing;
using System.Windows.Markup;
using System.Windows.Threading;
using WeatherApp.Pages;
using Esri.ArcGISRuntime.Data;
using Org.BouncyCastle.Bcpg.Sig;
using System.Globalization;
//using Microsoft.Office.Interop.Word;

namespace WeatherApp
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly List<AirQualityData> _airQualityData = new List<AirQualityData>();
        private GraphicsOverlay _graphicsOverlay;
        private readonly DispatcherTimer _refreshTimer = new DispatcherTimer();
        private readonly AirQualityService _airQualityService = new AirQualityService();
        private bool _isMapVisible = true;
        public MainWindow()
        {
            InitializeComponent();
            InitializeMap();
            SetupRefreshTimer();
        }
        private void SetupRefreshTimer()
        {
            _refreshTimer.Interval = TimeSpan.FromMinutes(2);
            _refreshTimer.Tick += async (s, e) => await RefreshData();
            _refreshTimer.Start();
        }
        private async Task RefreshData()
        {
            try
            {
                var data = await _airQualityService.GetDataAsync();
                UpdateMapMarkers(data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Refresh error: {ex.Message}");
            }
        }
        private void UpdateMapMarkers(List<AirQualityData> data)
        {
            _graphicsOverlay.Graphics.Clear();

            foreach (var item in data)
            {
                int aqi = item.AQI;
                var color = GetColorForAQI(aqi);
                var symbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, color, 12)
                {
                    Outline = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.White, 1)
                };
                var point = new MapPoint(item.Longitude, item.Latitude, SpatialReferences.Wgs84);
                var graphic = new Graphic(point, symbol);
                graphic.Attributes["Id"] = item.SensorId;
                graphic.Attributes["AQI"] = aqi;
                graphic.Attributes["Lon"] = item.Longitude;
                graphic.Attributes["Lat"] = item.Latitude;
                graphic.Attributes["PM25"] = item.PM25;
                graphic.Attributes["PM10"] = item.PM10;
                graphic.Attributes["Temperature"] = item.Temperature;
                graphic.Attributes["Humidity"] = item.Humidity;
                _graphicsOverlay.Graphics.Add(graphic);
            }
        }

        private async void InitializeMap()
        {
            try
            {
                Map myMap = new Map(BasemapStyle.ArcGISNavigation);
                MapPoint minskCenter = new MapPoint(27.5618, 53.9045, SpatialReferences.Wgs84);
                myMap.InitialViewpoint = new Viewpoint(minskCenter, 1000000);
                MyMapView.Map = myMap;
                await myMap.LoadAsync();
                _graphicsOverlay = new GraphicsOverlay();
                MyMapView.GraphicsOverlays.Add(_graphicsOverlay);
                // Загрузка и отображение данных
                await LoadAllAirQualityData();
                MyMapView.GeoViewTapped += MapView_Tapped;
                // DisplayAllMarkers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации карты: {ex.Message}");
            }
        }

        public static async Task<string> GetStreetNameWithArcGIS(double lat, double lon)
        {

            var point = new MapPoint(lon, lat, SpatialReferences.Wgs84);
            var results = await geocoder.ReverseGeocodeAsync(point);

            return results.FirstOrDefault()?.Attributes["Address"]?.ToString()
                   ?? results.FirstOrDefault()?.Attributes["Street"]?.ToString()
                   ?? "Неизвестная улица";
        }

        private async Task LoadAllAirQualityData()
        {
                using (var client = new HttpClient())
                {
                    try
                    {
                        var response = await client.GetStringAsync(apiUrl);
                        var json = JArray.Parse(response);

                        foreach (var sensor in json)
                        {
                            string country = sensor["country"]?.ToString();
                            double lat = sensor["location"]?["latitude"]?.ToObject<double>() ?? 0;
                            double lon = sensor["location"]?["longitude"]?.ToObject<double>() ?? 0;
                            string sensorId = sensor["sensor"]?["id"]?.ToString();
                            string location = sensor["location"]?["name"]?.ToString();
                            string name = sensor["station"]?["name"]?.ToString() ?? "";
                            string sensorType = sensor["sensor"]?["sensor_type"]?["name"]?.ToString();
                            string sensorPin = sensor["sensor"]?["pin"]?.ToString();


                            DateTime? timestamp = null;
                            if (DateTime.TryParse(sensor["timestamp"]?.ToString(), out var parsedTime))
                            {
                                timestamp = parsedTime;
                            }

                            Dictionary<string, string> measurements = new Dictionary<string, string>();

                            if (sensor["sensordatavalues"] is JArray sensordatavalues)
                            {
                                foreach (var measurement in sensordatavalues)
                                {
                                    string valueType = measurement["value_type"]?.ToString();
                                    string value = measurement["value"]?.ToString();

                                    if (!string.IsNullOrEmpty(valueType))
                                    {
                                        measurements[valueType] = value;
                                    }
                                }
                            }

                            measurements.TryGetValue("P2", out var pm25);
                            measurements.TryGetValue("P1", out var pm10);
                            measurements.TryGetValue("temperature", out var temperature);
                            measurements.TryGetValue("pressure", out var pressure);
                            measurements.TryGetValue("humidity", out var humidity);

                            int aqi = CalculateAQI(pm25, pm10);

                            var color = GetColorForAQI(aqi);
                            var symbol = new SimpleMarkerSymbol(SimpleMarkerSymbolStyle.Circle, color, 12)
                            {
                                Outline = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.White, 1)
                            };

                            var point = new MapPoint(lon, lat, SpatialReferences.Wgs84);
                            var graphic = new Graphic(point, symbol)
                            {
                                Attributes =
                                {
                                    ["Id"] = sensorId,
                                    ["Name"] = name,
                                    ["AQI"] = aqi,
                                    ["Lon"] = lon,
                                    ["Lat"] = lat,
                                    ["PM25"] = pm25,
                                    ["PM10"] = pm10,
                                    ["Temperature"] = temperature,
                                    ["Pressure"] = pressure,
                                    ["Humidity"] = humidity
                                }
                            };

                            _graphicsOverlay.Graphics.Add(graphic);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}");
                    }
                }
        }

        private async void MapView_Tapped(object sender, GeoViewInputEventArgs e)
        {
            try
            {
                var identifyResult = await MyMapView.IdentifyGraphicsOverlayAsync(
                    _graphicsOverlay,
                    e.Position,
                    15,
                    false);
                if (identifyResult.Graphics.Count > 0)
                {
                    var graphic = identifyResult.Graphics[0];
                    var id = GetAttributeValue(graphic, "SensorId", "Id");
                    var city = GetAttributeValue(graphic, "City", "city");
                    var aqi = GetAttributeValue(graphic, "AQI", "aqi");
                    var lon = GetAttributeValue(graphic, "Lon", "longitude", "lon");
                    var lat = GetAttributeValue(graphic, "Lat", "latitude", "lat");
                    var pm25 = GetAttributeValue(graphic, "PM25", "pm25", "P2");
                    var pm10 = GetAttributeValue(graphic, "PM10", "pm10", "P1");
                    var temp = GetAttributeValue(graphic, "Temperature", "temp", "temperature");
                    var humidity = GetAttributeValue(graphic, "Humidity", "humidity");         
                    var slidePanel = new Border
                    {
                        Background = new SolidColorBrush(Colors.White),
                        BorderBrush = new SolidColorBrush(Colors.LightGray),
                        BorderThickness = new Thickness(1),
                        Width = 320,
                        Padding = new Thickness(10),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                        VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                        Effect = new DropShadowEffect
                        {
                            Color = Colors.Black,
                            Direction = 270,
                            ShadowDepth = 5,
                            Opacity = 0.5
                        }
                    };
                    var stackPanel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Children =
                    {
                    new TextBlock
                    {
                        Text = id,
                        FontWeight = FontWeights.Bold,
                        FontSize = 16,
                        Margin = new Thickness(0,0,0,10)
                    },
                    new TextBlock
                    {
                        Text = $"Ожидаемые файлы данных:\n{id}_PM25.csv\n{id}_PM10.csv",
                        FontStyle = FontStyles.Italic,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        Margin = new Thickness(0,0,0,10),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock { Text = $"Индекс AQI: {aqi}" },
                    new TextBlock { Text = $"Координаты: {lat}, {lon}" },
                    new TextBlock { Text = $"Индекс pm10: {pm10}" },
                    new TextBlock { Text = $"Индекс pm25: {pm25}" },
                    new TextBlock { Text = $"Индекс температуры: {temp}" },
                    new TextBlock { Text = $"Индекс влажности: {humidity}" },
                    new Button
                    {
                        Content = "Закрыть",
                        Margin = new Thickness(0,20,0,0),
                        Padding = new Thickness(5),
                        HorizontalAlignment =  System.Windows.HorizontalAlignment.Right
                    },
                    new Button
                    {
                         Content = "Прогноз",
                        Margin = new Thickness(0,10,0,0),
                        Padding = new Thickness(5),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        Tag = id 
                    }
                    }
                };

                    slidePanel.Child = new ScrollViewer { 
                        Content = stackPanel };
                    var closeButton = ((slidePanel.Child as ScrollViewer).Content as StackPanel).Children
                        .OfType<Button>().FirstOrDefault();

                    if (closeButton != null)
                    {
                        closeButton.Click += (s, args) =>
                        {
                            var anim = new DoubleAnimation
                            {
                                To = -300,
                                Duration = TimeSpan.FromSeconds(0.3),
                                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                            };
                            slidePanel.RenderTransform.BeginAnimation(TranslateTransform.XProperty, anim);
                        };
                    }
                    if (FindName("SlidePanelGrid") is System.Windows.Controls.Grid slidePanelGrid)
                    {
                        slidePanelGrid.Children.Clear();
                        slidePanelGrid.Children.Add(slidePanel);

                        // Анимация появления
                        slidePanel.RenderTransform = new TranslateTransform(-300, 0);
                        var anim = new DoubleAnimation
                        {
                            To = 0,
                            Duration = TimeSpan.FromSeconds(0.3),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        slidePanel.RenderTransform.BeginAnimation(TranslateTransform.XProperty, anim);

                        var forecastButton = ((slidePanel.Child as ScrollViewer).Content as StackPanel).Children
                            .OfType<Button>().FirstOrDefault(b => (string)b.Content == "Прогноз");

                        if (forecastButton != null)
                        {
                            forecastButton.Click += (s, args) =>
                            {
                                try
                                {
                                    var stationName = (string)((Button)s).Tag ?? "Минск";
                                    string coordinates = (!string.IsNullOrEmpty(lat) && !string.IsNullOrEmpty(lon))
                                    ? $"{lat},{lon}"
            :                       "53.9,27.5667"; 
                                    var forecastPage = new ForecastWindow(stationName, coordinates);
                                    var anim1 = new DoubleAnimation
                                    {
                                        To = -300,
                                        Duration = TimeSpan.FromSeconds(0.3),
                                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                                    };
                                    slidePanel.RenderTransform.BeginAnimation(TranslateTransform.XProperty, anim1);


                                    if (System.Windows.Application.Current.MainWindow is MainWindow mainWin)
                                    {
                                        mainWin.MainFrame.Navigate(forecastPage);
                                        SwitchToFrameView();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Не удалось открыть прогноз: {ex.Message}");
                                }
                            };
                        }
                    }
                    else
                    {
                        MessageBox.Show("Ошибка: SlidePanelGrid не найден в XAML!");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при клике: {ex.Message}");
            }
        }
        private string GetAttributeValue(Graphic graphic, params string[] possibleKeys)
        {
            foreach (var key in possibleKeys)
            {
                if (graphic.Attributes.ContainsKey(key) && graphic.Attributes[key] != null)
                {
                    return graphic.Attributes[key].ToString();
                }
            }
            return "Нет данных";
        }

        public int CalculateAQI(string pm25Str, string pm10Str)
        {
            if (float.TryParse(pm25Str, out float pm25) && float.TryParse(pm10Str, out float pm10))
            {
                float maxPm = Math.Max(pm25, pm10);

                if (maxPm <= 12) return (int)(50 * (maxPm / 12));
                if (maxPm <= 35.4) return (int)(50 + 50 * ((maxPm - 12) / (35.4 - 12)));
                if (maxPm <= 55.4) return (int)(100 + 50 * ((maxPm - 35.4) / (55.4 - 35.4)));
                if (maxPm <= 150.4) return (int)(150 + 50 * ((maxPm - 55.4) / (150.4 - 55.4)));
                if (maxPm <= 250.4) return (int)(200 + 100 * ((maxPm - 150.4) / (250.4 - 150.4)));
                if (maxPm <= 350.4) return (int)(300 + 100 * ((maxPm - 250.4) / (350.4 - 250.4)));
                if (maxPm <= 500.4) return (int)(400 + 100 * ((maxPm - 350.4) / (500.4 - 350.4)));
                return 500;
            }

            return 0;
        }

        private System.Drawing.Color GetColorForAQI(int aqi)
        {
            if (aqi < 50)
                return System.Drawing.Color.Green; 
            else if (50 < aqi && aqi < 100)
                return System.Drawing.Color.Yellow;
            else if (100 < aqi && aqi < 150)
                return System.Drawing.Color.Orange;
            else if (150 < aqi && aqi < 200)
                return System.Drawing.Color.Red;
            else if (200 < aqi && aqi < 300)
                return System.Drawing.Color.Purple;
            else if (aqi > 300)
                return System.Drawing.Color.Maroon;
            else
                return System.Drawing.Color.DarkGreen;
        }

        public class AirQualityData
        {
            public string City { get; set; }
            public double Longitude { get; set; }
            public double Latitude { get; set; }
            public int AQI { get; set; }
            public double PM25 { get; set; }
            public double PM10 { get; set; }
            public double Temperature { get; set; }
            public double Humidity { get; set; }
            public string SensorId { get; set; }
            public string Location { get; set; }
            public DateTime Timestamp { get; set; }
            public bool IsForecast { get; set; }
            public bool IsHistorical { get; set; }
            public string CoordinatesString =>
        $"{Latitude.ToString(CultureInfo.InvariantCulture)},{Longitude.ToString(CultureInfo.InvariantCulture)}";
            public string DataType => IsForecast ? "Прогноз" : IsHistorical ? "История" : "Текущие";
        }
        private void RefreshData_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadAllAirQualityData();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void MapStyleStandard_Click(object sender, RoutedEventArgs e)
        {
            MyMapView.Map = new Map(BasemapStyle.ArcGISNavigation);
        }

        private void MapStyleSatellite_Click(object sender, RoutedEventArgs e)
        {
            MyMapView.Map = new Map(BasemapStyle.ArcGISImageryStandard);
        }

        private void MapStyleDark_Click(object sender, RoutedEventArgs e)
        {
            MyMapView.Map = new Map(BasemapStyle.ArcGISDarkGray);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Настройки приложения", "Параметры");
        }
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (MyMapView.MapScale > 1000)
            {
                MyMapView.SetViewpointScaleAsync(MyMapView.MapScale / 1.5);
            }
        }
        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            MyMapView.SetViewpointScaleAsync(MyMapView.MapScale * 1.5);
        }
        private void MapStyleChanged(object sender, SelectionChangedEventArgs e)
        {
            MessageBox.Show("Выбор сделан");
            if (MapStyleCombo.SelectedItem is ComboBoxItem item)
            {
                string style = item.Tag?.ToString();
                switch(style)
                { 
                    case "Navigation":
                        ChangeBaseMap(BasemapStyle.ArcGISNavigation);
                        break;

                    case "Imagery":
                        ChangeBaseMap(BasemapStyle.ArcGISImageryStandard);
                        break;

                    case "DarkGray":
                        ChangeBaseMap(BasemapStyle.ArcGISDarkGray);
                        break;
                }
            }
        }
        public void ChangeBaseMap(BasemapStyle newStyle)
        {
            MyMapView.Map = new Map(newStyle);
        }
        private void ShowForecast_Click(object sender, RoutedEventArgs e)
        {
            _isMapVisible = false;
            if (FindName("SlidePanelGrid") is System.Windows.Controls.Grid slidePanelGrid)
                slidePanelGrid.Children.Clear();
            try
            {
                string currentLocation = "Sensor-вуліца-Бельскага";
                string coordinates = "53.9,27.5667";
                var forecastWindow = new ForecastWindow(currentLocation, coordinates);

                MainFrame.Navigate(forecastWindow);
                SwitchToFrameView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть прогноз: {ex.Message}",
                               "Ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }

        private void SwitchToFrameView()
        {
            _isMapVisible = false;
            MyMapView.Visibility = Visibility.Collapsed;
            MainFrame.Visibility = Visibility.Visible;
        }
        private void MapButton_Click(object sender, RoutedEventArgs e)
        {
            ClearNavigationHistory();
            SwitchToMapView();
        }
        private void SwitchToMapView()
        {
            _isMapVisible = true;
            MainFrame.Visibility = Visibility.Collapsed;
            MyMapView.Visibility = Visibility.Visible;
            ClearNavigationHistory();
        }
        private void ClearNavigationHistory()
        {
            while (MainFrame.CanGoBack)
            {
                MainFrame.RemoveBackEntry();
            }
            MainFrame.Content = null;
        }
        private void ShowAdmin_Click(object sender, RoutedEventArgs e)
        {
            _isMapVisible = false;
            try
            {
                var adminWindow = new AdminWindow();

                adminWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть окно админа: {ex.Message}",
                               "Ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _isMapVisible = false;
            if (FindName("SlidePanelGrid") is System.Windows.Controls.Grid slidePanelGrid)
                slidePanelGrid.Children.Clear();
            try
            {
                var alertUserControl = new UserAlert();
                MainFrame.Navigate(alertUserControl);
                SwitchToFrameView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть окно админа: {ex.Message}",
                               "Ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }

        private void Events_Click(object sender, RoutedEventArgs e)
        {
            _isMapVisible = false;
            if (FindName("SlidePanelGrid") is System.Windows.Controls.Grid slidePanelGrid)
                slidePanelGrid.Children.Clear();
            try
            {
                var measureUserControl = new Measure();
                MainFrame.Navigate(measureUserControl);
                SwitchToFrameView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть окно админа: {ex.Message}",
                               "Ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }
        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateUserState();
        }

        public void UpdateUserState()
        {
            if (!string.IsNullOrEmpty(SessionManager.CurrentUserEmail))
            {
                UserInfoText.Text = SessionManager.CurrentUserEmail;
                UserInfoText.Visibility = Visibility.Visible;

                AccountButton.Content = "Аккаунт";
            }
            else
            {
                UserInfoText.Text = "";
                UserInfoText.Visibility = Visibility.Collapsed;

                AccountButton.Content = "Войти";
            }
        }

        private void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SessionManager.CurrentUserEmail))
            {
                var accountWindow = new AccountWindow();
                accountWindow.ShowDialog();
                // обновим отображение (если был выход)
                UpdateUserState();
            }
            else
            {
                var loginWindow = new LoginWindow(this);
                if (loginWindow.ShowDialog() == true)
                {
                    UpdateUserState();
                }
            }
        }
    }
}
