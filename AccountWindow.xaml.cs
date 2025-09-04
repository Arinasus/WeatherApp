using MahApps.Metro.Controls;
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
using System.Windows.Shapes;
using WeatherApp.Pages;

namespace WeatherApp
{
    /// <summary>
    /// Логика взаимодействия для AccountWindow.xaml
    /// </summary>
    public partial class AccountWindow : MetroWindow
    {
        public string WelcomeText { get; set; }

        public AccountWindow()
        {
            InitializeComponent();
            WelcomeText = $"Вы вошли как: {SessionManager.CurrentUserEmail}";
            DataContext = this;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            SessionManager.Logout();
            SessionManager.CurrentUserEmail = null;
            var mainWindow = Application.Current.MainWindow as MainWindow;
            var loginWindow = new LoginWindow(mainWindow);
            loginWindow.Show();
            this.Close();

        }
    }
}
