using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeatherApp
{
    public static class SessionManager
    {
        public static event Action OnLogout;
        public static string CurrentUserEmail { get; set; } = null;
        public static HashSet<int> JoinedEventIds { get; } = new HashSet<int>();
        public static void Logout()
        {
            CurrentUserEmail = null;
            JoinedEventIds.Clear();
            OnLogout?.Invoke();
        }
    }
}
