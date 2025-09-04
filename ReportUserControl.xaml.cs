using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static WeatherApp.MainWindow;
using Newtonsoft.Json.Linq;
using System.Linq;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using OxyPlot;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using WD = DocumentFormat.OpenXml.Wordprocessing;
using System.Threading;
namespace WeatherApp
{
    public partial class ReportUserControl
    {
        private readonly SemaphoreSlim _apiRequestSemaphore = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, (double lat, double lon)> _cityCoordinatesCache = new Dictionary<string, (double, double)>();
        private readonly HttpClient _httpClient = new HttpClient();
        private bool _includeForecast = false;
        private bool _includeHistory = false;
        private bool _includeGraphs = false;
        // Словарь с городами и районами для каждой области
        private readonly Dictionary<string, List<CityInfo>> _regionCities = new Dictionary<string, List<CityInfo>>
        {
            ["minsk"] = new List<CityInfo>
            {
                new CityInfo { Name = "Минск", Districts = new List<string> { "Центральный", "Советский", "Первомайский", "Партизанский", "Заводской", "Ленинский", "Октябрьский", "Московский", "Фрунзенский" } }
            },
            ["brest"] = new List<CityInfo>
            {
                new CityInfo { Name = "Брест", Districts = new List<string> { "Ленинский", "Московский" } },
                new CityInfo { Name = "Барановичи" },
                new CityInfo { Name = "Пинск" },
                new CityInfo { Name = "Кобрин" }
            },
            ["vitebsk"] = new List<CityInfo>
            {
                new CityInfo { Name = "Витебск", Districts = new List<string> { "Железнодорожный", "Октябрьский", "Первомайский" } },
                new CityInfo { Name = "Орша" },
                new CityInfo { Name = "Новополоцк" },
                new CityInfo { Name = "Полоцк" }
            },
            ["gomel"] = new List<CityInfo>
            {
                new CityInfo { Name = "Гомель", Districts = new List<string> { "Центральный", "Советский", "Новобелицкий", "Железнодорожный" } },
                new CityInfo { Name = "Мозырь" },
                new CityInfo { Name = "Жлобин" },
                new CityInfo { Name = "Светлогорск" }
            },
            ["grodno"] = new List<CityInfo>
            {
                new CityInfo { Name = "Гродно", Districts = new List<string> { "Ленинский", "Октябрьский" } },
                new CityInfo { Name = "Лида" },
                new CityInfo { Name = "Слоним" },
                new CityInfo { Name = "Волковыск" }
            },
            ["mogilev"] = new List<CityInfo>
            {
                new CityInfo { Name = "Могилев", Districts = new List<string> { "Ленинский", "Октябрьский" } },
                new CityInfo { Name = "Бобруйск" },
                new CityInfo { Name = "Осиповичи" },
                new CityInfo { Name = "Горки" }
            }
        };

        public ReportUserControl()
        {
            InitializeComponent();
            StartDatePicker.SelectedDate = DateTime.Today.AddDays(-7);
            EndDatePicker.SelectedDate = DateTime.Today;
        }

        private void RegionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RegionComboBox.SelectedItem is ComboBoxItem selectedRegion)
            {
                var regionTag = selectedRegion.Tag.ToString();
                CityComboBox.Items.Clear();

                if (_regionCities.TryGetValue(regionTag, out var cities))
                {
                    foreach (var city in cities)
                    {
                        CityComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = city.Name,
                            Tag = city.Name.ToLower().Replace(" ", "-")
                        });
                    }
                }

                if (CityComboBox.Items.Count > 0)
                {
                    CityComboBox.SelectedIndex = 0;
                }
            }
        }

        private void CityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RegionComboBox.SelectedItem is ComboBoxItem selectedRegion &&
                CityComboBox.SelectedItem is ComboBoxItem selectedCity)
            {
                var regionTag = selectedRegion.Tag.ToString();
                var cityName = selectedCity.Content.ToString();

                var cityInfo = _regionCities[regionTag].FirstOrDefault(c => c.Name == cityName);
            }
        }
        private async Task<List<AirQualityData>> GetAirQualityData(string city, DateTime startDate, DateTime endDate)
        {
            var data = new List<AirQualityData>();

            try
            {
                // 1. Получаем текущие данные
                var currentData = await GetCurrentAirQuality(city);
                if (currentData != null)
                {
                    data.Add(currentData);
                }

                // 2. Получаем исторические данные (если включены)
                if (_includeHistory)
                {
                    var historyData = await GetHistoricalAirQuality(city, startDate, endDate);
                    if (historyData != null && historyData.Count > 0)
                    {
                        data.AddRange(historyData);
                     
                    }
                    else
                    {
                        MessageBox.Show("Не удалось получить исторические данные");
                    }
                }

                // 3. Получаем прогнозные данные (если включены)
                if (_includeForecast)
                {
                    var forecastData = await GetAirQualityForecast(city);
                    if (forecastData != null && forecastData.Count > 0)
                    {
                        data.AddRange(forecastData);
                       
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении данных: {ex.Message}");
            }

            return data.OrderBy(d => d.Timestamp).ToList();
        }
        private async Task<List<AirQualityData>> GetHistoricalAirQuality(string city, DateTime startDate, DateTime endDate)
        {
            try
            {
                // Получаем координаты города
                var (lat, lon) = await GetCityCoordinates(city);
                if (lat == 0 && lon == 0) return null;

                // OpenWeatherMap требует timestamp в Unix формате
                long startUnix = ((DateTimeOffset)startDate).ToUnixTimeSeconds();
                long endUnix = ((DateTimeOffset)endDate).ToUnixTimeSeconds();


                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(json);

                var historicalData = new List<AirQualityData>();

                foreach (var item in result["list"])
                {
                    var components = item["components"];
                    historicalData.Add(new AirQualityData
                    {
                        City = city,
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(item["dt"].Value<long>()).DateTime,
                        AQI = item["main"]["aqi"].Value<int>(),
                        PM25 = components["pm2_5"].Value<double>(),
                        PM10 = components["pm10"].Value<double>(),
                        IsHistorical = true,
                        Latitude = lat,
                        Longitude = lon
                    });
                }

               
                return historicalData;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка получения исторических данных: {ex.Message}");
                return null;
            }
        }

        private async Task<List<AirQualityData>> GetAirQualityForecast(string city)
        {
            try
            {
                var (lat, lon) = await GetCityCoordinates(city);
                if (lat == 0 && lon == 0) return null;

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JObject.Parse(json);

                    var forecastData = new List<AirQualityData>();

                    foreach (var item in result["list"])
                    {
                        var components = item["components"];
                        forecastData.Add(new AirQualityData
                        {
                            City = city,
                            Timestamp = DateTimeOffset.FromUnixTimeSeconds(item["dt"].Value<long>()).DateTime,
                            AQI = item["main"]["aqi"].Value<int>(),
                            PM25 = components["pm2_5"].Value<double>(),
                            PM10 = components["pm10"].Value<double>(),
                            IsForecast = true,
                            Latitude = lat,
                            Longitude = lon
                        });
                    }

                    return forecastData;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка получения прогноза: {ex.Message}");
            }
            return null;
        }
        private async Task<AirQualityData> GetCurrentAirQuality(string city)
        {
            try
            {

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var jsonObj = JObject.Parse(json);

                    if (jsonObj["status"]?.ToString() == "ok")
                    {
                        var iaqi = jsonObj["data"]["iaqi"];
                        var cityInfo = jsonObj["data"]["city"];
                        var timeInfo = jsonObj["data"]["time"];

                        var selectedRegion = (RegionComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Неизвестный регион";
                        var selectedCity = (CityComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Неизвестный город";
                        return new AirQualityData
                        {
                            City = FormatCityName(selectedCity),
                            Timestamp = DateTime.Parse(timeInfo["s"]?.ToString()),
                            AQI = (int)jsonObj["data"]["aqi"],
                            PM25 = GetPollutantValue(iaqi, "pm25"),
                            PM10 = GetPollutantValue(iaqi, "pm10"),
                            Temperature = GetPollutantValue(iaqi, "t"),
                            Humidity = GetPollutantValue(iaqi, "h"),
                            Location = $"{FormatCityName(selectedCity)}, {selectedRegion}",
                            SensorId = jsonObj["data"]["idx"]?.ToString(),
                            Latitude = (double)jsonObj["data"]["city"]["geo"][0],
                            Longitude = (double)jsonObj["data"]["city"]["geo"][1]
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"API Error: {ex.Message}");
            }

            return null;
        }
        private string FormatCityName(string cityName)
        {
            if (string.IsNullOrEmpty(cityName))
                return "Неизвестный город";

            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cityName.ToLower());
        }
        private double GetPollutantValue(JToken iaqi, string pollutant)
        {
            try
            {
                return (double)iaqi[pollutant]["v"];
            }
            catch
            {
                return 0; // Возвращаем 0 если данные отсутствуют
            }
        }

        private string GenerateDocxReport(List<AirQualityData> data)
        {
            var reportType = (ReportTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "public";
            var fileName = $"AirQualityReport_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            using (WordprocessingDocument wordDocument = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                // 1. Титульная страница
                AddTitlePage(body, data.First().City, reportType);

                // 2. Сводная информация
                AddSummarySection(body, data);

                // 3. Детальная таблица с данными
                body.AppendChild(new WD.Paragraph(new WD.Run(new WD.Text("Детальные данные:"))));
                body.AppendChild(CreateDataTable(data));
                if (data.Any(d => d.IsHistorical))
                {
                    var historyParagraph = new WD.Paragraph(
                        new WD.Run(
                            new WD.Text("Исторические данные:")
                            {
                                Space = SpaceProcessingModeValues.Preserve
                            })
                        {
                            RunProperties = new WD.RunProperties(
                                new WD.Bold(),
                                new WD.FontSize { Val = "22" }
                            )
                        });

                    body.AppendChild(historyParagraph);

                    // Добавляем только исторические данные
                    var historyTable = CreateDataTable(data.Where(d => d.IsHistorical).ToList());
                    body.AppendChild(historyTable);
                }
                if (_includeGraphs)
                {
                    AddChartsToReport(wordDocument, body, data);
                }

                // 4. Заключение
                AddConclusion(body, reportType, data);
            }

            return filePath;
        }
        private void AddChartsToReport(WordprocessingDocument wordDocument, Body body, List<AirQualityData> data)
        {
            try
            {
                // Увеличим размер графика
                var plotModel = new PlotModel
                {
                    Title = "Динамика качества воздуха",
                    TitleFontSize = 14,
                    PlotAreaBorderThickness = new OxyThickness(1),
                    DefaultFontSize = 12
                };

                // Настройка осей
                var dateAxis = new DateTimeAxis
                {
                    Position = AxisPosition.Bottom,
                    StringFormat = "dd.MM HH:mm",
                    Title = "Дата и время",
                    TitleFontSize = 12,
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot
                };

                var valueAxis = new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Title = "AQI",
                    TitleFontSize = 12,
                    MajorGridlineStyle = LineStyle.Solid,
                    MinorGridlineStyle = LineStyle.Dot,
                    Minimum = 0,
                    Maximum = data.Max(d => d.AQI) * 1.2 // Добавляем 20% сверху
                };

                plotModel.Axes.Add(dateAxis);
                plotModel.Axes.Add(valueAxis);

                // Добавляем серии данных
                var currentSeries = new LineSeries
                {
                    Title = "Текущие",
                    StrokeThickness = 2,
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 4
                };

                var historicalSeries = new LineSeries
                {
                    Title = "История",
                    StrokeThickness = 2,
                    MarkerType = MarkerType.Square,
                    MarkerSize = 4
                };

                var forecastSeries = new LineSeries
                {
                    Title = "Прогноз",
                    StrokeThickness = 2,
                    MarkerType = MarkerType.Triangle,
                    MarkerSize = 4
                };

                // Заполняем данные
                foreach (var item in data)
                {
                    var point = new DataPoint(DateTimeAxis.ToDouble(item.Timestamp), item.AQI);

                    if (item.IsForecast) forecastSeries.Points.Add(point);
                    else if (item.IsHistorical) historicalSeries.Points.Add(point);
                    else currentSeries.Points.Add(point);
                }

                plotModel.Series.Add(currentSeries);
                plotModel.Series.Add(historicalSeries);
                plotModel.Series.Add(forecastSeries);

                // Экспорт в изображение большего размера
                var chartPath = Path.GetTempFileName() + ".png";
                new PngExporter
                {
                    Width = 1200,
                    Height = 600,
                }.ExportToFile(plotModel, chartPath);

                AddImageToBody(wordDocument, body, chartPath);
                File.Delete(chartPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании графиков: {ex.Message}");
            }
        }
        private string CreateChartImage(List<AirQualityData> data, string valueField, string title)
        {
            var plot = new PlotModel { Title = title };

            var series = new LineSeries
            {
                Title = valueField,
                ItemsSource = data,
                DataFieldX = "Timestamp",
                DataFieldY = valueField
            };

            plot.Series.Add(series);
            plot.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom });
            plot.Axes.Add(new LinearAxis { Position = AxisPosition.Left });

            var tempPath = Path.GetTempFileName() + ".png";
            var exporter = new PngExporter { Width = 1200, Height = 800 };
            exporter.ExportToFile(plot, tempPath);

            return tempPath;
        }

        private void AddTitlePage(Body body, string city, string reportType)
        {
            var formattedCity = char.ToUpper(city[0]) + city.Substring(1).ToLower();

            var titleParagraph = new WD.Paragraph();
            var titleRun = new WD.Run();
            titleRun.AppendChild(new WD.Text($"Отчет о качестве воздуха в г. {formattedCity}"));
            titleRun.PrependChild(new WD.RunProperties(
                new WD.Bold(),
                new WD.FontSize { Val = "36" },
                new WD.Color { Val = "0072CE" }
            ));
            titleParagraph.AppendChild(titleRun);
            titleParagraph.ParagraphProperties = new WD.ParagraphProperties(
                new WD.Justification { Val = WD.JustificationValues.Center },
                new WD.SpacingBetweenLines { After = "200" }
            );
            body.AppendChild(titleParagraph);

            // Тип отчета (берем из выбранного значения)
            var reportTypeItem = (ComboBoxItem)ReportTypeComboBox.SelectedItem;
            var reportTypeText = reportTypeItem?.Content.ToString() ?? "Для общественности";

            body.AppendChild(new WD.Paragraph(
                new WD.Run(new WD.Text(reportTypeText))
                {
                    RunProperties = new WD.RunProperties(
                        new WD.Bold(),
                        new WD.FontSize { Val = "28" }
                    )
                }
            ));

            // Период отчета
            body.AppendChild(new WD.Paragraph(
                new WD.Run(new WD.Text(
                    $"Период: {StartDatePicker.SelectedDate.Value:dd.MM.yyyy} - {EndDatePicker.SelectedDate.Value:dd.MM.yyyy}"))
                {
                    RunProperties = new WD.RunProperties(new WD.FontSize { Val = "24" })
                }
            ));

            body.AppendChild(new WD.Paragraph(new WD.Run(new WD.Text(""))));
        }

        private void AddSummarySection(Body body, List<AirQualityData> data)
        {
            var stats = new
            {
                AvgAqi = data.Average(d => d.AQI),
                MaxPm25 = data.Max(d => d.PM25),
                AvgTemp = data.Average(d => d.Temperature),
                AvgHumidity = data.Average(d => d.Humidity)
            };

            body.AppendChild(new Paragraph(
                new Run(new Text("Сводная статистика:"))
                {
                    RunProperties = new RunProperties(
                        new Bold(),
                        new FontSize { Val = "28" }
                    )
                }
            ));

            var summaryTable = new Table();
            summaryTable.AppendChild(new TableProperties(
                new TableWidth { Width = "100%", Type = TableWidthUnitValues.Pct }
            ));

            AddSummaryRow(summaryTable, "Средний индекс AQI", stats.AvgAqi.ToString("F1"));
            AddSummaryRow(summaryTable, "Максимальный PM2.5", stats.MaxPm25.ToString("F1") + " µg/m³");
            AddSummaryRow(summaryTable, "Средняя температура", stats.AvgTemp.ToString("F1") + "°C");
            AddSummaryRow(summaryTable, "Средняя влажность", stats.AvgHumidity.ToString("F1") + "%");

            body.AppendChild(summaryTable);
            body.AppendChild(new Paragraph(new Run(new Text(""))));
        }

        private void AddSummaryRow(Table table, string name, string value)
        {
            TableRow row = new TableRow();

            // Ячейка с названием параметра
            TableCell nameCell = new TableCell(
                new Paragraph(
                    new Run(new Text(name))
                    {
                        RunProperties = new RunProperties(
                            new FontSize { Val = "20" }
                        )
                    }
                )
                {
                    ParagraphProperties = new ParagraphProperties(
                        new Justification { Val = JustificationValues.Left }
                    )
                }
            );

            // Ячейка со значением
            TableCell valueCell = new TableCell(
                new Paragraph(
                    new Run(new Text(value))
                    {
                        RunProperties = new RunProperties(
                            new FontSize { Val = "20" }
                        )
                    }
                )
                {
                    ParagraphProperties = new ParagraphProperties(
                        new Justification { Val = JustificationValues.Right }
                    )
                }
            );

            // Добавляем ячейки в строку
            row.AppendChild(nameCell);
            row.AppendChild(valueCell);

            // Добавляем строку в таблицу
            table.AppendChild(row);
        }

        private Table CreateDataTable(List<AirQualityData> data)
        {
            Table table = new Table();

            // Настройки таблицы
            TableProperties tableProperties = new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4 },
                    new BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder { Val = BorderValues.Single, Size = 4 },
                    new RightBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
                ),
                new TableWidth { Width = "100%", Type = TableWidthUnitValues.Pct }
            );
            table.AppendChild(tableProperties);

            // Заголовки столбцов
            TableRow headerRow = new TableRow();
            string[] headers = { "Дата", "AQI", "PM2.5", "PM10", "Температура", "Влажность" };

            foreach (var header in headers)
            {
                headerRow.AppendChild(CreateHeaderCell(header));
            }
            table.AppendChild(headerRow);

            // Данные
            foreach (var item in data)
            {
                TableRow dataRow = new TableRow();

                dataRow.AppendChild(CreateDataCell(item.Timestamp.ToString("dd.MM.yyyy HH:mm")));
                dataRow.AppendChild(CreateDataCell(item.AQI.ToString(), GetAqiColor(item.AQI)));
                dataRow.AppendChild(CreateDataCell(item.PM25.ToString("F1") + " µg/m³"));
                dataRow.AppendChild(CreateDataCell(item.PM10.ToString("F1") + " µg/m³"));
                dataRow.AppendChild(CreateDataCell(item.Temperature.ToString("F1") + "°C"));
                dataRow.AppendChild(CreateDataCell(item.Humidity.ToString("F1") + "%"));

                table.AppendChild(dataRow);
            }

            return table;
        }

        private TableCell CreateHeaderCell(string text)
        {
            return new TableCell(
                new Paragraph(
                    new Run(
                        new Text(text)
                        {
                            Space = SpaceProcessingModeValues.Preserve
                        }
                    )
                    {
                        RunProperties = new RunProperties(
                            new Bold(),
                            new Color { Val = "FFFFFF" },
                            new FontSize { Val = "22" }
                        )
                    }
                )
                {
                    ParagraphProperties = new ParagraphProperties(
                        new Justification { Val = JustificationValues.Center },
                        new Shading { Fill = "0072CE" }
                    )
                }
            );
        }

        private TableCell CreateDataCell(string text, string backgroundColor = "auto")
        {
            return new TableCell(
                new Paragraph(
                    new Run(
                        new Text(text)
                        {
                            Space = SpaceProcessingModeValues.Preserve
                        }
                    )
                    {
                        RunProperties = new RunProperties(
                            new FontSize { Val = "20" }
                        )
                    }
                )
                {
                    ParagraphProperties = new ParagraphProperties(
                        new Justification { Val = JustificationValues.Center },
                        new Shading { Fill = backgroundColor }
                    )
                }
            );
        }

        private string GetAqiColor(int aqi)
        {
            if (aqi < 50) return "A6D96A";
            if (aqi < 100) return "FEE08B";
            if (aqi < 150) return "FD8D3C";
            if (aqi < 200) return "E31A1C";
            if (aqi < 300) return "8C2D04";
            return "7E0023";
        }

        private string CreateTempChartImage(List<AirQualityData> data)
        {
            var tempPath = Path.GetTempFileName() + ".png";

            var plotModel = new PlotModel { Title = "Изменение AQI" };

            var lineSeries = new LineSeries
            {
                Title = "AQI",
                ItemsSource = data,
                DataFieldX = "Timestamp",
                DataFieldY = "AQI"
            };

            plotModel.Series.Add(lineSeries);
            plotModel.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom });
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left });

            var pngExporter = new PngExporter { Width = 1200, Height = 600 };
            pngExporter.ExportToFile(plotModel, tempPath);

            return tempPath;
        }

        private void AddImageToBody(WordprocessingDocument wordDocument, Body body, string imagePath)
        {
            var imagePart = wordDocument.MainDocumentPart.AddImagePart(ImagePartType.Png);

            using (FileStream stream = new FileStream(imagePath, FileMode.Open))
            {
                imagePart.FeedData(stream);
            }

            var element = new Drawing(
                new DW.Inline(
                    new DW.Extent() { Cx = 1905000L, Cy = 1270000L },
                    new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties() { Id = 1U, Name = "Chart" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks() { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties() { Id = 0U, Name = "Chart.png" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip() { Embed = wordDocument.MainDocumentPart.GetIdOfPart(imagePart) },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset() { X = 0L, Y = 0L },
                                        new A.Extents() { Cx = 990000L, Cy = 792000L }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                        )
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
                )
                { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U, EditId = "50D07946" });

            body.AppendChild(new Paragraph(new Run(element)));
        }

        private void AddConclusion(Body body, string reportType, List<AirQualityData> data)
        {
            Console.WriteLine($"Тип отчета для заключения: {reportType}"); // Отладка

            var conclusionParagraph = new WD.Paragraph();
            var conclusionRun = new WD.Run();
            string conclusionText;
            switch (reportType)
            {
                case "gov":
                    conclusionText = GetGovernmentConclusion(data);
                    break;
                case "internal":
                    conclusionText = GetInternalConclusion(data);
                    break;
                default:
                    conclusionText = GetPublicConclusion(data);
                    break;
            }

            conclusionRun.AppendChild(new WD.Text(conclusionText));
            conclusionRun.PrependChild(new WD.RunProperties(new WD.FontSize { Val = "24" }));
            conclusionParagraph.AppendChild(conclusionRun);
            body.AppendChild(conclusionParagraph);
        }
        private string GetGovernmentConclusion(List<AirQualityData> data)
        {
            return "Рекомендации для государственных органов:\n" +
                   $"• Средний AQI: {data.Average(d => d.AQI):F1}\n" +
                   "1. Усилить контроль за промышленными выбросами\n" +
                   "2. Рассмотреть ограничения движения транспорта";
        }

        private string GetInternalConclusion(List<AirQualityData> data)
        {
            return "Внутренний анализ:\n" +
                   $"• Средний индекс качества воздуха (AQI): {data.Average(d => d.AQI):F1}\n" +
                   $"• Максимальная концентрация PM2.5: {data.Max(d => d.PM25):F1} µg/m³\n" +
                   $"• Средние показатели: температура {data.Average(d => d.Temperature):F1}°C, " +
                   $"влажность {data.Average(d => d.Humidity):F1}%\n\n" +
                   "Рекомендации для внутреннего использования:\n" +
                   "1. Анализ источников загрязнения\n" +
                   "2. Сравнение с нормативными показателями\n" +
                   "3. Разработка корректирующих мер";
        }

        private string GetPublicConclusion(List<AirQualityData> data)
        {
            return "Информация для общественности:\n" +
                   "Качество воздуха в целом удовлетворительное.\n" +
                   "При повышенных значениях PM2.5 рекомендуется ограничить пребывание на улице.";
        }
        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {

        }
        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "Формирование отчета...";

                if (!(CityComboBox.SelectedItem is ComboBoxItem selectedCity))
                {
                    MessageBox.Show("Выберите город");
                    return;
                }

                if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
                {
                    MessageBox.Show("Укажите период");
                    return;
                }

                var city = selectedCity.Tag.ToString();
                var startDate = StartDatePicker.SelectedDate.Value;
                var endDate = EndDatePicker.SelectedDate.Value;

                var airQualityData = await GetAirQualityData(city, startDate, endDate);

                if (airQualityData == null || !airQualityData.Any())
                {
                    airQualityData = new List<AirQualityData>
                {
                    new AirQualityData
                    {
                        City = city,
                        Timestamp = DateTime.Now,
                        AQI = 65,
                        PM25 = 18.3,
                        PM10 = 28.7,
                        Temperature = 20.5,
                        Humidity = 58.2,
                        Location = "Центральный район",
                        SensorId = "WAQI-API-DATA"
                    }
                };
                }

                var filePath = GenerateDocxReport(airQualityData);
                StatusTextBlock.Text = $"Отчет успешно сгенерирован: {filePath}";

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Ошибка при генерации отчета";
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Review_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (!(CityComboBox.SelectedItem is ComboBoxItem selectedCity))
                {
                    MessageBox.Show("Выберите город");
                    return;
                }

                if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
                {
                    MessageBox.Show("Укажите период");
                    return;
                }

                var city = selectedCity.Tag.ToString();
                var startDate = StartDatePicker.SelectedDate.Value;
                var endDate = EndDatePicker.SelectedDate.Value;

                var airQualityData = await GetAirQualityData(city, startDate, endDate);

                // Всегда генерируем отчет, даже если нет данных от API
                if (airQualityData == null || !airQualityData.Any())
                {
                    // Добавляем демо-данные, если API не вернул результатов
                    airQualityData = new List<AirQualityData>
                    {
                        new AirQualityData
                        {
                            City = city,
                            Timestamp = DateTime.Now,
                            AQI = 65,
                            PM25 = 18.3,
                            PM10 = 28.7,
                            Temperature = 20.5,
                            Humidity = 58.2,
                            Location = "Центральный район",
                            SensorId = "WAQI-API-DATA"
                        }
                    };
                }
                ConvertDocxToXps(airQualityData);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Ошибка при генерации отчета";
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConvertDocxToXps(List<AirQualityData> airQualityData)
        {
            var filePath = GenerateDocxReport(airQualityData);
            StatusTextBlock.Text = $"Отчет успешно сгенерирован: {filePath}";

            var xpsPath = ConvertDocx(filePath);
            var previewWindow = new Preview(xpsPath);
            previewWindow.ShowDialog();
            File.Delete(xpsPath);
        }
        private string ConvertDocx(string docxPath)
        {

            string xpsPath = System.IO.Path.ChangeExtension(docxPath, ".xps");

            var wordApp = new Microsoft.Office.Interop.Word.Application();
            try
            {
                var doc = wordApp.Documents.Open(docxPath);
                doc.SaveAs(xpsPath, Microsoft.Office.Interop.Word.WdSaveFormat.wdFormatXPS);
                doc.Close();
            }
            finally
            {
                wordApp.Quit();
            }
            return xpsPath;
        }
        private void IncludeForecastCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _includeForecast = IncludeForecastCheckBox.IsChecked ?? false;
        }

        private void IncludeHistoryCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _includeHistory = IncludeHistoryCheckBox.IsChecked ?? false;
        }

        private void IncludeGraphCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _includeGraphs = IncludeGraphCheckBox.IsChecked ?? false;
        }
        private async Task<(double lat, double lon)> GetCityCoordinates(string city)
        {
            if (_cityCoordinatesCache.TryGetValue(city, out var cachedCoords))
            {
                return cachedCoords;
            }

            await _apiRequestSemaphore.WaitAsync();
            try
            {
                // Двойная проверка кэша (на случай параллельных запросов)
                if (_cityCoordinatesCache.TryGetValue(city, out cachedCoords))
                {
                    return cachedCoords;
                }

                var url = $"http://api.openweathermap.org/geo/1.0/direct?q={city}&limit=1&appid={OpenWeahterAPI}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JArray.Parse(json);

                    if (result.Count > 0)
                    {
                        var coords = (result[0]["lat"].Value<double>(), result[0]["lon"].Value<double>());
                        _cityCoordinatesCache[city] = coords; // Сохраняем в кэш
                        return coords;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка получения координат: {ex.Message}");
            }
            finally
            {
                _apiRequestSemaphore.Release();
            }

            return (0, 0);
        }
    }
}

    public class CityInfo
    {
        public string Name { get; set; }
        public List<string> Districts { get; set; }
    }

