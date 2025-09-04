using Esri.ArcGISRuntime.Location;
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
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using Esri.ArcGISRuntime.Mapping.Labeling;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Newtonsoft.Json;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Win32;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfExporterNew = OxyPlot.Pdf.PdfExporter;

namespace WeatherApp
{
    /// <summary>
    /// Логика взаимодействия для ForecastWindow.xaml
    /// </summary>
    public partial class ForecastWindow : System.Windows.Controls.Page
    {
        private string _currentParameter = "PM25";
        private List<AirQualityCsvRecord> _records;
        private string _location;
        private string _streetName;
        private string _coordinates;
        public ForecastWindow(string location, string coordinates)
        {
            InitializeComponent();
            _location = location;
            _coordinates = coordinates;
              PM25Button.Click += async (s, e) =>
              {
                  _currentParameter = "PM25";
                  PM25Button.IsEnabled = false;
                  PM10Button.IsEnabled = true;
                  await LoadData();
                  var forecastRecords = await GetForecastDataAsync(_coordinates, _currentParameter);
                  ShowForecastPlot(forecastRecords);
              };

              PM10Button.Click += async (s, e) =>
              {
                  _currentParameter = "PM10";
                  PM10Button.IsEnabled = false;
                  PM25Button.IsEnabled = true;
                  await LoadData();
                  var forecastRecords = await GetForecastDataAsync(_coordinates, _currentParameter);
                  ShowForecastPlot(forecastRecords);
              };

              // Устанавливаем активную кнопку
              PM25Button.IsEnabled = _currentParameter != "PM25";
              PM10Button.IsEnabled = _currentParameter != "PM10";
              Loaded += async (s, e) =>
              {
                  await LoadData(); 
                  var forecastRecords = await GetForecastDataAsync(_coordinates, _currentParameter);
                  if (forecastRecords != null && forecastRecords.Count > 0)
                  {
                      ShowForecastPlot(forecastRecords);
                  }
                  else
                  {
                      MessageBox.Show("Не удалось получить данные прогноза или они пустые");
                  }
              };
        }
        private string NormalizeCoordinates(string input)
        {
            input = input.Replace(" ", "");
            var parts = input.Split(',');

            if (parts.Length == 4) // Если формат "53,946,27,634"
            {
                return $"{parts[0]}.{parts[1]},{parts[2]}.{parts[3]}";
            }
            else if (parts.Length == 2) // Если формат уже "53.946,27.634"
            {
                return input; // Оставляем как есть
            }

            return input; // На всякий случай возвращаем исходное значение
        }
        private async Task<List<AQRecord>> GetForecastFromCoordinates(double lat, double lon, string parameter)
        {
            try
            {

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode(); // Выбросит исключение, если статус не 200-299

                    string json = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<AirPollutionForecastResponse>(json);

                    if (data?.list == null || data.list.Count == 0)
                    {
                        MessageBox.Show("Нет данных в ответе API");
                        return new List<AQRecord>();
                    }

                    var result = new List<AQRecord>();
                    foreach (var item in data.list)
                    {
                        if (item.components == null) continue;

                        result.Add(new AQRecord
                        {
                            Date = DateTimeOffset.FromUnixTimeSeconds(item.dt).DateTime,
                            Value = parameter == "PM25" ? item.components.pm2_5 : item.components.pm10
                        });
                    }

                    return result;
                }
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"Ошибка HTTP: {ex.Message}");
                return new List<AQRecord>();
            }
            catch (System.Text.Json.JsonException ex)
            {
                MessageBox.Show($"Ошибка JSON: {ex.Message}");
                return new List<AQRecord>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
                return new List<AQRecord>();
            }
        }
        private void UpdateStationInfo()
        {
            try
            {
                StationNameText.Text = $"Станция: {_location}";
                LastUpdateText.Text = $"Обновлено: {DateTime.Now:dd.MM.yyyy HH:mm}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при обновлении информации: {ex}");
            }
        }

        private async Task<List<AQRecord>> GetForecastDataAsync(string coordinates, string parameter)
        {
             /*var parts = coordinates.Split(',');
             if (parts.Length != 2)
                 return new List<AQRecord>();*/
            coordinates = NormalizeCoordinates(_coordinates);
            var parts = coordinates.Split(',');

            if (!double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double lat))
                return new List<AQRecord>();
            if (!double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double lon))
                return new List<AQRecord>();

            string apiKey = OpenWeatherAPI;

            HttpClient client = new HttpClient();
            string response = await client.GetStringAsync(url);

            dynamic json = JsonConvert.DeserializeObject(response);
            var list = json.list;

            var records = new List<AQRecord>();

            foreach (var item in list)
            {
                long dt = item.dt;
                DateTime date = DateTimeOffset.FromUnixTimeSeconds(dt).DateTime;
                double value = parameter == "PM25"
                    ? (double)item.components.pm2_5
                    : (double)item.components.pm10;

                records.Add(new AQRecord { Date = date, Value = value });
            }

            return records;
        }
        private string ExtractStreetNameFromCsv(string filePath)
        {
            try
            {
                var lines = File.ReadLines(filePath, Encoding.GetEncoding(1251));
                var firstLine = lines.FirstOrDefault(l => l.StartsWith("# Sensor"));

                if (firstLine != null)
                {
                    var match = Regex.Match(firstLine, @"# Sensor\s*([^,\r\n]+)");
                    if (match.Success)
                    {
                        string streetName = match.Groups[1].Value.Trim();
                        streetName = streetName.Split(new[] { '(', '[' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();

                        return streetName;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка извлечения названия: {ex.Message}");
            }
            return _location;
        }

        private async Task LoadData()
        {
            try
            {
                ProgressBar.Visibility = Visibility.Visible;
                ForecastStatusText.Text = "Загрузка данных...";
                await Task.Delay(300); // Имитация загрузки

                string fileName = $"{_location}_{_currentParameter}.csv".Replace(" ", "_");
                string filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "AirQuality", fileName);
                if (!File.Exists(filePath))
                {
                    HistoryStatusText.Text = $"Файл данных не найден: {fileName}";
                    HistoryPlot.Visibility = Visibility.Collapsed;
                    ForecastPlot.Model = null;
                    return;
                }
                _streetName = ExtractStreetNameFromCsv(filePath);
                StationNameText.Text = $"Станция: {_streetName} (Файл данных: {fileName})";
                LastUpdateText.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                StationNameText.Language = System.Windows.Markup.XmlLanguage.GetLanguage("be-BY");

                _records = ReadCsvData(filePath);
                if (_records.Count == 0)
                {
                    MessageBox.Show("Нет данных в CSV.");
                    HistoryPlot.Model = null;
                    return;
                }
                else {  HistoryStatusText.Text = string.Empty; }
                    UpdatePlot();
                ForecastStatusText.Text = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
                ProgressBar.Visibility = Visibility.Collapsed;
            }
            finally
            {
                ForecastStatusText.Text = string.Empty;
              
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }
        private void UpdatePlot()
        {
            if (_records == null || _records.Count == 0) return;

            string title = _currentParameter == "PM25" ? "PM2.5" : "PM10";
            string unit = "µg/m³";
            string fileName = $"{_location}_{_currentParameter}.csv".Replace(" ", "_");
            string filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "AirQuality", fileName);
            _streetName = ExtractStreetNameFromCsv(filePath);
            var model = new PlotModel { Title = $"{title} (median) — {_streetName}" };

            var barSeries = new RectangleBarSeries
            {
                FillColor = _currentParameter == "PM25" ? OxyColors.Red : OxyColors.Blue,
                StrokeThickness = 0
            };

            foreach (var r in _records)
            {
                double center = DateTimeAxis.ToDouble(r.Date);
                double value = _currentParameter == "PM25" ? r.Median : r.Median;

                barSeries.Items.Add(new RectangleBarItem
                {
                    X0 = center - 0.5 / 2,
                    X1 = center + 0.5 / 2,
                    Y0 = 0,
                    Y1 = value
                });
            }

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "dd.MM",
                Title = "Дата",
                Font = "Arial",
                TitleFont = "Arial",
                IntervalType = DateTimeIntervalType.Days,
                MinorIntervalType = DateTimeIntervalType.Days,
                Angle = 45
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = $"{title} {unit}",
                Font = "Arial",
                TitleFont = "Arial",
                FontSize = 10,
                TitleFontSize = 12
            });

            model.Series.Add(barSeries);
            HistoryPlot.Model = model;
        }
        private List<AirQualityCsvRecord> ReadCsvData(string filePath)
        {
            var records = new List<AirQualityCsvRecord>();

            using (var reader = new StreamReader(filePath))
            {
                // Пропускаем строки, начинающиеся с #
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (!line.StartsWith("#"))
                    {
                        var csvText = line + "\n" + reader.ReadToEnd();

                        using (var csvReader = new CsvReader(new StringReader(csvText), new CsvConfiguration(CultureInfo.InvariantCulture)
                        {
                            HasHeaderRecord = true,
                            IgnoreBlankLines = true,
                            PrepareHeaderForMatch = args => args.Header.ToLower()
                        }))
                        {
                            records = csvReader.GetRecords<AirQualityCsvRecord>().ToList();
                        }
                        break;
                    }
                }
            }

            return records;
        }
        private async Task<List<AQRecord>> GetForecastFromOpenWeather()
        {
            try
            {
                string[] parts = _coordinates.Split(',');
                string lat = parts[0];
                string lon = parts[1];

                HttpClient client = new HttpClient();
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Не удалось получить данные прогноза.");
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                var forecast = JsonConvert.DeserializeObject<AirPollutionForecastResponse>(json);

                var result = forecast.list
                    .GroupBy(item => DateTimeOffset.FromUnixTimeSeconds(item.dt).DateTime.Date)
                    .Select(g =>
                    {
                        var date = g.Key;
                        var values = _currentParameter == "PM25"
                            ? g.Select(x => x.components.pm2_5)
                            : g.Select(x => x.components.pm10);

                        return new AQRecord
                        {
                            Date = date,
                            Value = values.Average()
                        };
                    })
                    .Take(5)
                    .ToList();

                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка получения прогноза: {ex.Message}");
                return null;
            }
        }
        private void ShowForecastPlot(List<AQRecord> forecastRecords)
        {
            if (forecastRecords == null || forecastRecords.Count == 0)
            {
                MessageBox.Show("Нет данных для отображения");
                return;
            }

            var model = new PlotModel { Title = $"Прогноз: {_currentParameter}" };
            double norm = _currentParameter == "PM25" ? 25.0 : 50.0;

            var barSeries = new RectangleBarSeries
            {
                StrokeThickness = 0
            };

            foreach (var record in forecastRecords)
            {
                double center = DateTimeAxis.ToDouble(record.Date);
                double value = record.Value;
                OxyColor color;
                if (value <= norm)
                    color = OxyColors.Green;
                else if (value <= norm * 1.5)
                    color = OxyColors.Orange;
                else
                    color = OxyColors.Red;

                // Добавляем столбец
                barSeries.Items.Add(new RectangleBarItem
                {
                    X0 = center - 0.5 / 2,
                    X1 = center + 0.5 / 2,
                    Y0 = 0,
                    Y1 = value,
                    Color = color
                });
            }
            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Дата и время",
                StringFormat = "dd.MM HH:mm",
                IntervalType = DateTimeIntervalType.Hours,
                MinorIntervalType = DateTimeIntervalType.Hours,
                Angle = 45,
                TitleFontSize = 12,
                FontSize = 10
            });
            double maxValue = _records?.Any() == true
     ? _records.Max(r => _currentParameter == "PM25" ? r.Median : r.Median)
     : 0;
            // Ось концентрации
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = $"{_currentParameter} (µg/m³)",
                Minimum = 0,
                TitleFontSize = 12,
                FontSize = 10
            });

            model.Series.Add(barSeries);
            ForecastPlot.Model = model;
        }
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                await LoadData();
                var forecastRecords = await GetForecastDataAsync(_coordinates, _currentParameter);
                if (forecastRecords != null && forecastRecords.Count > 0)
                    ShowForecastPlot(forecastRecords);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при обновлении данных:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
        private void ExportToPdf(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF-файл (*.pdf)|*.pdf",
                Title = "Сохранить как PDF",
                FileName = "AirQualityReport.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Экспорт двух графиков во временные PDF-потоки
                    using (var tempStream1 = new MemoryStream())
                    using (var tempStream2 = new MemoryStream())
                    {
                        var exporter = new OxyPlot.Pdf.PdfExporter { Width = 600, Height = 400 };

                        exporter.Export(HistoryPlot.Model, tempStream1);
                        exporter.Export(ForecastPlot.Model, tempStream2);

                        // Перематываем в начало потоков
                        tempStream1.Position = 0;
                        tempStream2.Position = 0;

                        // Создаём итоговый PDF и добавляем страницы из потоков
                        var outputDoc = new PdfSharp.Pdf.PdfDocument();

                        var inputDoc1 = PdfSharp.Pdf.IO.PdfReader.Open(tempStream1, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
                        var inputDoc2 = PdfSharp.Pdf.IO.PdfReader.Open(tempStream2, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);

                        outputDoc.AddPage(inputDoc1.Pages[0]);
                        outputDoc.AddPage(inputDoc2.Pages[0]);

                        // Сохраняем результат
                        outputDoc.Save(dialog.FileName);

                        MessageBox.Show("PDF успешно сохранён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при сохранении PDF:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
    public class AQRecord
    {
        public DateTime Date { get; set; }
        public double Value { get; set; }
    }
    public class HourlyAirQuality
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }
    public class AirQualityCsvRecord
    {
        public DateTime Date { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Median { get; set; }
        public double Q1 { get; set; }
        public double Q3 { get; set; }
        public double Stdev { get; set; }
        public int Count { get; set; }
    }
    public class AirPollutionForecastResponse
    {
        public List<AirPollutionData> list { get; set; }
    }

    public class AirPollutionData
    {
        public MainInfo main { get; set; }
        public AirComponents components { get; set; }
        public long dt { get; set; }
    }

    public class MainInfo
    {
        public int aqi { get; set; }
    }

    public class AirComponents
    {
        public double pm2_5 { get; set; }
        public double pm10 { get; set; }
    }
}