using MahApps.Metro.Controls;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WeatherApp.Pages
{
    /// <summary>
    /// Логика взаимодействия для LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : MetroWindow
    {
        private MainWindow _main;
        public LoginWindow(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            // Добавляем обработчики для валидации в реальном времени
            EmailBox.TextChanged += (s, e) => ValidateFields();
            PasswordBox.PasswordChanged += (s, e) => ValidateFields();

            LoginButton.IsEnabled = false;
        }
        private void ValidateFields()
        {
            string email = EmailBox.Text.Trim();
            string password = PasswordBox.Password;

            bool isValid = true;

            // Валидация email
            if (string.IsNullOrWhiteSpace(email))
            {
                EmailError.Text = "Email обязателен для заполнения";
                isValid = false;
            }
            else if (!Regex.IsMatch(email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                EmailError.Text = "Некорректный формат email";
                isValid = false;
            }
            else
            {
                EmailError.Text = "";
            }

            // Валидация пароля
            if (string.IsNullOrWhiteSpace(password))
            {
                PasswordError.Text = "Пароль обязателен для заполнения";
                isValid = false;
            }
            else
            {
                PasswordError.Text = "";
            }

            LoginButton.IsEnabled = isValid;
        }
        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginButton.IsEnabled)
            {
                MessageBox.Show("Пожалуйста, исправьте ошибки в форме");
                return;
            }

            string email = EmailBox.Text.Trim();
            string password = PasswordBox.Password;

            var db = new DBWorking();
            LoginButton.IsEnabled = false;

            try
            {
                var authResult = await Task.Run(() => db.AuthenticateUser(email, password));

                if (authResult == AuthResult.Success)
                {
                    SessionManager.CurrentUserEmail = email;
                    _main.UpdateUserState();

                    if (email.ToLower() == "admin@admin.com")
                    {
                        var adminWindow = new AdminWindow();
                        adminWindow.Show();
                    }
                    else
                    {
                        var accountWindow = new AccountWindow();
                        accountWindow.Show();
                    }

                    this.Close();
                }
                else if (authResult == AuthResult.UserNotFound)
                {
                    EmailError.Text = "Пользователь с таким email не найден";
                }
                else if (authResult == AuthResult.WrongPassword)
                {
                    PasswordError.Text = "Неверный пароль";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при авторизации: {ex.Message}");
            }
            finally
            {
                LoginButton.IsEnabled = true;
            }
        }
       
        private void RegisterHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var registerWindow = new RegisterWindow(_main);
            if (registerWindow.ShowDialog() == true) // модальное
            {
                // Можно обновить интерфейс или открыть AccountWindow
                _main.UpdateUserState();
            }
            this.Close();
        }

    }
    public enum AuthResult
    {
        Success,
        UserNotFound,
        WrongPassword,
        Error
    }
}
