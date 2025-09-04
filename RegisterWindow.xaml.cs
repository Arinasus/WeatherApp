using DocumentFormat.OpenXml.Spreadsheet;
using MahApps.Metro.Controls;
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
using WeatherApp.Pages;

namespace WeatherApp
{
    /// <summary>
    /// Логика взаимодействия для RegisterWindow.xaml
    /// </summary>
    public partial class RegisterWindow : MetroWindow
    {
        private MainWindow _main;
        public RegisterWindow(MainWindow main)
        {
            InitializeComponent();
            _main = main;



            EmailBox.TextChanged += (s, e) => ValidateFields();
            PasswordBox.PasswordChanged += (s, e) => ValidateFields();
            ConfirmPasswordBox.PasswordChanged += (s, e) => ValidateFields();

            RegisterButton.IsEnabled = false;
        }
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            if (!RegisterButton.IsEnabled)
            {
                MessageBox.Show("Пожалуйста, исправьте ошибки в форме");
                return;
            }

            string email = EmailBox.Text.Trim();
            string password = PasswordBox.Password;

            var db = new DBWorking();
            db.InitializeUsersTable();
            if (db.RegisterUser(email, password))
            {
                SessionManager.CurrentUserEmail = email;
                _main?.UpdateUserState();
                var accountWindow = new AccountWindow();
                accountWindow.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Пользователь с таким email уже существует.");
            }
        }
        private void ValidateFields()
        {
            string email = EmailBox.Text.Trim();
            string password = PasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;

            bool isValid = true;

            // Валидация email
            if (string.IsNullOrWhiteSpace(email))
            {
                EmailError.Text = "Email обязателен для заполнения";
                isValid = false;
            }
            else if (!Regex.IsMatch(email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                EmailError.Text = "Некорректный формат email (только английские символы)";
                isValid = false;
            }
            else if (email.Length < 5 || email.Length > 50)
            {
                EmailError.Text = "Email должен быть от 5 до 50 символов";
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
            else if (password.Length < 8 || password.Length > 50)
            {
                PasswordError.Text = "Пароль должен быть от 8 до 50 символов";
                isValid = false;
            }
            else if (!Regex.IsMatch(password, @"[A-Z]"))
            {
                PasswordError.Text = "Пароль должен содержать хотя бы одну заглавную букву";
                isValid = false;
            }
            else if (!Regex.IsMatch(password, @"[0-9]"))
            {
                PasswordError.Text = "Пароль должен содержать хотя бы одну цифру";
                isValid = false;
            }
            else if (!Regex.IsMatch(password, @"[!@#$%^&*()_+=\[{\]};:<>|./?,-]"))
            {
                PasswordError.Text = "Пароль должен содержать хотя бы один спецсимвол";
                isValid = false;
            }
            else
            {
                PasswordError.Text = "";
            }

            // Подтверждение пароля
            if (string.IsNullOrWhiteSpace(confirmPassword))
            {
                ConfirmPasswordError.Text = "Подтверждение пароля обязательно";
                isValid = false;
            }
            else if (password != confirmPassword)
            {
                ConfirmPasswordError.Text = "Пароли не совпадают";
                isValid = false;
            }
            else
            {
                ConfirmPasswordError.Text = "";
            }

            RegisterButton.IsEnabled = isValid;
        }
        private void LoginHyperlink_Click(object sender, RoutedEventArgs e)
        {     
            var loginWindow = new LoginWindow(_main);
            loginWindow.Show();
            this.Close();
        }
    }
}
