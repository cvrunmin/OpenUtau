using OpenUtau.Lang;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace OpenUtau
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        App()
        {
            InitializeComponent();
        }

        [STAThread]
        static void Main()
        {
            //Thread backgroundThread = new Thread(new ThreadStart(() => { }));
            //backgroundThread.Start();

            Core.DocManager.Inst.SearchAllSingers();
            var pm = new OpenUtau.Core.PartManager();
            App app = new App();
            LoadLanguage();
            UI.MainWindow window = new UI.MainWindow();
            app.Run(window);
        }

        private static void LoadLanguage()
        {
            LanguageManager.Add("en-us", "pack://application:,,,/Lang/en-us.xaml");
            LanguageManager.Add("zh-cht", "pack://application:,,,/Lang/zh-cht.xaml");

            LanguageManager.UseLanguage(Core.Util.Preferences.Default.Language);
        }
    }
}
