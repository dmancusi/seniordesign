using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace BigData {
    class App : Application {
        [STAThread]
        static void Main(string[] args) {
            try {
                var a = new App();
                
                a.MainWindow = new UI.MainWindow();
                a.Run();
            } catch (Exception ex) {
                MessageBox.Show(ex.StackTrace, ex.Message);
            }
        }
    }
}
