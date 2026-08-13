using System;
using System.Windows;
using RenderPard.UI;

namespace TestApp {
    class Program {
        [STAThread]
        static void Main() {
            var app = new App();
            app.InitializeComponent();
            try {
                var w = new SettingsWindow();
                Console.WriteLine("Success");
            } catch (Exception ex) {
                Console.WriteLine("Exception: " + ex.ToString());
                if (ex.InnerException != null) Console.WriteLine("Inner: " + ex.InnerException.ToString());
            }
        }
    }
}
