using System;
using System.Windows;

namespace TestApp {
    class Program {
        [STAThread]
        static void Main() {
            var app = new Application();
            try {
                var img = RenderPard.UI.IconGenerator.GetIconImageSource("Test");
                Console.WriteLine("Success: " + (img != null));
            } catch (Exception ex) {
                Console.WriteLine("Error: " + ex.ToString());
            }
        }
    }
}
