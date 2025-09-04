using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static WeatherApp.MainWindow;

namespace WeatherApp
{
    public class AirQualityService
    {
        private List<AirQualityData> _cachedData;
        private DateTime _lastUpdateTime;
        public async Task<List<AirQualityData>> GetDataAsync(bool forceRefresh = false)
        {
            // Если данные актуальны (менее 2 минут) и не требуется принудительное обновление
            if (_cachedData != null && !forceRefresh &&
                (DateTime.Now - _lastUpdateTime).TotalMinutes < 2)
            {
                return _cachedData;
            }

            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetStringAsync(API_URL);
                    _cachedData = ParseResponse(response);
                    _lastUpdateTime = DateTime.Now;
                }
                return _cachedData;
            }
            catch
            {
                // В случае ошибки возвращаем кэш, если он есть
                return _cachedData ?? new List<AirQualityData>();
            }
        }

        private List<AirQualityData> ParseResponse(string json)
        {
            var result = new List<AirQualityData>();
            var jsonArray = JArray.Parse(json);

            foreach (var sensor in jsonArray)
            {
                try
                {
                    var data = new AirQualityData
                    {
                        SensorId = sensor["sensor"]?["id"]?.ToString(),
                        Location = sensor["location"]?["name"]?.ToString(),
                        Latitude = sensor["location"]?["latitude"]?.ToObject<double>() ?? 0,
                        Longitude = sensor["location"]?["longitude"]?.ToObject<double>() ?? 0,
                        Timestamp = DateTime.TryParse(sensor["timestamp"]?.ToString(), out var dt) ? dt : DateTime.MinValue
                    };

                    // Парсинг измерений
                    var measurements = sensor["sensordatavalues"] as JArray;
                    if (measurements != null)
                    {
                        foreach (var m in measurements)
                        {
                            string type = m["value_type"]?.ToString();
                            string value = m["value"]?.ToString();

                            switch (type)
                            {
                                case "P1": data.PM10 = ParseDouble(value); break;
                                case "P2": data.PM25 = ParseDouble(value); break;
                                case "temperature": data.Temperature = ParseDouble(value); break;
                                case "humidity": data.Humidity = ParseDouble(value); break;
                            }
                        }
                    }

                    data.AQI = CalculateAQI(data.PM25, data.PM10);
                    result.Add(data);
                }
                catch {
                    MessageBox.Show("Ошибка при обновлении");
                }
            }

            return result;
        }

        private double ParseDouble(string value) =>
            double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;

        public int CalculateAQI(double pm25, double pm10)
        {
            double maxPm = Math.Max(pm25, pm10);

            if (maxPm <= 12) return (int)(50 * (maxPm / 12));
                if (maxPm <= 35.4) return (int)(50 + 50 * ((maxPm - 12) / (35.4 - 12)));
                if (maxPm <= 55.4) return (int)(100 + 50 * ((maxPm - 35.4) / (55.4 - 35.4)));
                if (maxPm <= 150.4) return (int)(150 + 50 * ((maxPm - 55.4) / (150.4 - 55.4)));
                if (maxPm <= 250.4) return (int)(200 + 100 * ((maxPm - 150.4) / (250.4 - 150.4)));
                if (maxPm <= 350.4) return (int)(300 + 100 * ((maxPm - 250.4) / (350.4 - 250.4)));
                if (maxPm <= 500.4) return (int)(400 + 100 * ((maxPm - 350.4) / (500.4 - 350.4)));
                return 500;
        }
    }
}
