using System;
using System.Threading.Tasks;
using System.Windows;

namespace iRacingOverlay.Overlay
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}