using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Crypto.Generators;
using WeatherApp.Pages;

namespace WeatherApp
{   
    public class DBWorking
    {
        private ObservableCollection<NewsItem> NewsItems = new ObservableCollection<NewsItem>();

        public void InitializeUsersTable()
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                var command = new MySqlCommand(@"
            CREATE TABLE IF NOT EXISTS Users (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                Email VARCHAR(255) NOT NULL UNIQUE,
                PasswordHash VARCHAR(255) NOT NULL
            ) CHARACTER SET utf8mb4", connection);
                command.ExecuteNonQuery();
            }
        }
        public bool RegisterUser(string email, string password)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                // Проверка на существующего пользователя
                var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM Users WHERE Email = @Email", connection);
                checkCmd.Parameters.AddWithValue("@Email", email);
                var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
                if (exists) return false;

                // Сохраняем пароль в открытом виде (без хеширования)
                var insertCmd = new MySqlCommand(
                    "INSERT INTO Users (Email, PasswordHash) VALUES (@Email, @PasswordHash)", connection);
                insertCmd.Parameters.AddWithValue("@Email", email);
                insertCmd.Parameters.AddWithValue("@PasswordHash", password);
                insertCmd.ExecuteNonQuery();

                return true;
            }
        }

        public AuthResult AuthenticateUser(string email, string password)
        {
            // Проверка входных параметров
            if (string.IsNullOrWhiteSpace(email))
                return AuthResult.UserNotFound;

            if (string.IsNullOrWhiteSpace(password))
                return AuthResult.WrongPassword;

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    // Проверяем существование пользователя
                    var checkUserCmd = new MySqlCommand(
                        "SELECT COUNT(1) FROM Users WHERE Email = @email",
                        connection);
                    checkUserCmd.Parameters.AddWithValue("@email", email);

                    var userExists = Convert.ToInt32(checkUserCmd.ExecuteScalar()) > 0;
                    if (!userExists)
                        return AuthResult.UserNotFound;

                    // Получаем пароль из БД
                    var getPasswordCmd = new MySqlCommand(
                        "SELECT PasswordHash FROM Users WHERE Email = @email",
                        connection);
                    getPasswordCmd.Parameters.AddWithValue("@email", email);

                    var storedPassword = getPasswordCmd.ExecuteScalar()?.ToString();
                    if (storedPassword == null)
                        return AuthResult.UserNotFound;

                    // Простая проверка пароля без хеширования
                    if (storedPassword == password)
                        return AuthResult.Success;
                    else
                        return AuthResult.WrongPassword;
                }
            }
            catch (Exception ex)
            {
                // Логирование ошибки (можно заменить на вашу систему логгирования)
                Console.WriteLine($"Ошибка аутентификации: {ex.Message}");
                return AuthResult.Error;
            }
        }

        public void LoadDataForAlert()
        {
            var connection = new MySqlConnection(connectionString);
            connection.Open();

            var command = new MySqlCommand(@"
                create table if not exists News(
                id int auto_increment primary key,
                title varchar(30),
                content text,
                publish_date datetime,
                type varchar(50),
                priority varchar(20)
                ) CHARACTER SET utf8mb4", connection); 
            command.ExecuteNonQuery();
        }
        public void InsertNewsItem(NewsItem item)
        {
            var connection = new MySqlConnection(connectionString);
            connection.Open();

            var command = new MySqlCommand(@"
        INSERT INTO News (title, content, publish_date, type, priority)
        VALUES (@title, @content, @publish_date, @type, @priority)", connection);

            command.Parameters.AddWithValue("@title", item.Title);
            command.Parameters.AddWithValue("@content", item.Content);
            command.Parameters.AddWithValue("@publish_date", DateTime.Now);
            command.Parameters.AddWithValue("@type", item.Type.ToString());
            command.Parameters.AddWithValue("@priority", item.Priority.ToString());

            command.ExecuteNonQuery();
        }

        public void InitializeEventsTable()
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                var command = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS Events (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    Name VARCHAR(100) NOT NULL,
                    EventDate DATETIME NOT NULL,
                    Description TEXT
                ) CHARACTER SET utf8mb4", connection);
                command.ExecuteNonQuery();
            }
        }
        public bool AddEvent(string name, DateTime date, string description)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    var command = new MySqlCommand(
                        "INSERT INTO Events (Name, EventDate, Description) VALUES (@Name, @Date, @Description)",
                        connection);

                    command.Parameters.AddWithValue("@Name", name);
                    command.Parameters.AddWithValue("@Date", date);
                    command.Parameters.AddWithValue("@Description", description);

                    return command.ExecuteNonQuery() > 0;
                }
                catch (Exception ex)
                {
                    // Логирование ошибки
                    Console.WriteLine($"Ошибка при добавлении мероприятия: {ex.Message}");
                    return false;
                }
            }
        }

        // Получение всех мероприятий из БД
        public List<Event> GetAllEvents()
        {
            var events = new List<Event>();

            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    var command = new MySqlCommand("SELECT Id, Name, EventDate, Description FROM Events", connection);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            events.Add(new Event
                            {
                                Id = reader.GetInt32("Id"),
                                Name = reader.GetString("Name"),
                                Date = reader.GetDateTime("EventDate"),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                                    ? string.Empty
                                    : reader.GetString("Description")
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при получении мероприятий: {ex.Message}");
                }
            }

            return events;
        }
        public bool DeleteEvent(int eventId)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    var command = new MySqlCommand(
                        "DELETE FROM Events WHERE Id = @Id",
                        connection);

                    command.Parameters.AddWithValue("@Id", eventId);
                    return command.ExecuteNonQuery() > 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при удалении мероприятия: {ex.Message}");
                    return false;
                }
            }
        }
        public void InitializeParticipationsTable()
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                var command = new MySqlCommand(@"
            CREATE TABLE IF NOT EXISTS Participations (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                Email VARCHAR(255) NOT NULL,
                EventId INT NOT NULL,
                RegistrationDate DATETIME NOT NULL,
                FOREIGN KEY (EventId) REFERENCES Events(Id) ON DELETE CASCADE
            ) CHARACTER SET utf8mb4", connection);
                command.ExecuteNonQuery();
            }
        }
        public bool AddParticipation(string email, int eventId)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // Проверяем, есть ли уже такая запись
                    var checkCommand = new MySqlCommand(
                        "SELECT COUNT(*) FROM Participations WHERE Email = @Email AND EventId = @EventId",
                        connection);
                    checkCommand.Parameters.AddWithValue("@Email", email);
                    checkCommand.Parameters.AddWithValue("@EventId", eventId);

                    var exists = Convert.ToInt32(checkCommand.ExecuteScalar()) > 0;
                    if (exists) return false;

                    // Добавляем новую запись
                    var insertCommand = new MySqlCommand(
                        "INSERT INTO Participations (Email, EventId, RegistrationDate) " +
                        "VALUES (@Email, @EventId, @Date)", connection);

                    insertCommand.Parameters.AddWithValue("@Email", email);
                    insertCommand.Parameters.AddWithValue("@EventId", eventId);
                    insertCommand.Parameters.AddWithValue("@Date", DateTime.Now);

                    return insertCommand.ExecuteNonQuery() > 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при добавлении участника: {ex.Message}");
                    return false;
                }
            }
        }

        public class Participation
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
            public DateTime RegistrationDate { get; set; }
            public int EventId { get; set; }
        }
        public List<Participation> GetEventParticipations(int eventId)
        {
            var participations = new List<Participation>();

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                var command = new MySqlCommand(
                    "SELECT Id, Email, RegistrationDate FROM Participations WHERE EventId = @EventId",
                    connection);
                command.Parameters.AddWithValue("@EventId", eventId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        participations.Add(new Participation
                        {
                            Id = reader.GetInt32("Id"),
                            Email = reader.GetString("Email"),
                            RegistrationDate = reader.GetDateTime("RegistrationDate"),
                            EventId = eventId
                        });
                    }
                }
            }

            return participations;
        }




    }
}
