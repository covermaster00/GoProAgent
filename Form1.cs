using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Reflection; // Логирование

using System.Net.NetworkInformation; // Работа с сетевыми адаптерами
using System.Management;    // Работа с событиями

namespace GPAgent
{
    public partial class Form1 : Form
    {
        public bool Connecting = false;

        const int WM_DEVICECHANGE = 0x219;
        const int DBT_DEVICEARRIVAL = 0x8000;
        const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
        const int DBT_DEVTYP_VOLUME = 0x00000002;

        private BackgroundWorker backgroundWorker1;
        // Структура для передачи параметров в субпоток
        // https://professorweb.ru/my/WPF/documents_WPF/level31/31_3.php
        public class DownloadParameters
        {
            public string GoPro
            { get; set; }

            public DownloadParameters(string gopro)
            {
                GoPro = gopro;
            }

        }

        List<string> serials = new List<string>();

    //    private DriveDetector driveDetector = null;

        // Главная форма
        public Form1()
        {
            InitializeComponent();

            timer1.Stop();

            // Загрузка конфигурации из файла XML
            System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(XMLConfig));
            System.IO.StreamReader file = new System.IO.StreamReader(@"config.xml");
            Global.Config = (XMLConfig)reader.Deserialize(file);
            
            if (Global.Config.MakeWorkLogs) Logger.WriteLine("Конфигурация XML загружена");

            // Организация фоновой работы
            // https://learn.microsoft.com/ru-ru/dotnet/desktop/winforms/controls/how-to-download-a-file-in-the-background?view=netframeworkdesktop-4.8 
            backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            backgroundWorker1.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_RunWorkerCompleted);
            backgroundWorker1.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker1_ProgressChanged);


        }

        // Событие подключения GoPro
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);   

            // Определяем подключение USB устройства
            if ((m.Msg == WM_DEVICECHANGE) && !Connecting)
            //MessageBox.Show("USB");
            {
                
                timer1.Start();
                
                MessageBox.Show(m.WParam.ToString());
            }
         
        }


       
        // Основной фоновый процесс - скачивание файлов
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            // Получить входные значения
            DownloadParameters input = (DownloadParameters)e.Argument;

   //         MessageBox.Show(input.GoPro);


            // Получение страницы со списком файлов с камеры

            WebRequest request = WebRequest.Create(input.GoPro);
            request.Method = "GET";
            WebResponse response = request.GetResponse();
            if (Global.Config.MakeWorkLogs) Logger.WriteLine("Подключение камеры " + input.GoPro);

            string answer = string.Empty;
            using (Stream s = response.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(s))
                {
                    answer = reader.ReadToEnd();
                }
            }
            response.Close();


            // Парсинг html страницы, извлечение имен файлов
            List<string> names = new List<string>();
            for (var m = Regex.Match(answer, @"GX[0-9]{6}\.MP4"); m.Success; m = m.NextMatch())
            {
                names.Add(m.Value);
//                MessageBox.Show(m.Value);
            }
            // удаление дубликатов
            IEnumerable<string> distinctNames = names.AsQueryable().Distinct();

            // Папка назначения
            string destPath = Global.Config.DestPath;

            WebClient client = new WebClient();
            foreach (var name in distinctNames)
            {
                MessageBox.Show(input.GoPro + name);
                client.DownloadFile(new Uri(input.GoPro + name), destPath + name);
                if (Global.Config.MakeWorkLogs) Logger.WriteLine("Копирование файла " + input.GoPro + name);
   //             backgroundWorker1.ReportProgress(100 / distinctNames.Count());
                
            }
            
        }

        // Событие по завершении выполнения
        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // Set progress bar to 100% in case it's not already there.
            progressBar1.Value = 100;

            if (e.Error == null)
            {
                MessageBox.Show("Download Complete");
            }
            else
            {
                MessageBox.Show(
                    "Failed to download file",
                    "Download failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            progressBar1.Value = 0;
        }

        // Событие изменения в фоновой функции
        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.progressBar1.Value = e.ProgressPercentage;
        }

  

        // ОСНОВНАЯ КНОПКА
        private void button1_Click(object sender, EventArgs e)
        {

            // Блок модно удалить после тестов
            WebRequest request = WebRequest.Create(textBox1.Text);
            request.Method = "GET";
            WebResponse response = request.GetResponse();
            string answer = string.Empty;
            using (Stream s = response.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(s))
                {
                    answer = reader.ReadToEnd();
                }
            }
            response.Close();
            richTextBox1.Text = answer;

            // Вызываем копирование для данной GoPro
            LoadFromGoPro(textBox1.Text);

        }


        public void LoadFromGoPro(string goproUri)
        {
            DownloadParameters input = new DownloadParameters(goproUri);
            this.backgroundWorker1.RunWorkerAsync(input);
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        // Проверка наличия события и привязка камеры
        private void timer1_Tick(object sender, EventArgs e)
        {
            Connecting = true;

            

            timer1.Stop();
            Thread.Sleep(5000);
            
           
                //     MessageBox.Show("GOPRO");
                    foreach (var mo in new ManagementObjectSearcher(null, "SELECT * FROM Win32_PnPEntity").Get().OfType<ManagementObject>())
                    {
                        var argsd = new object[] { new string[] { "DEVPKEY_Device_FriendlyName", "DEVPKEY_Device_LastKnownParent" }, null };
                        // or this works too using the PK's value formatted as string
                        //var argsd = new object[] { new string[] { "DEVPKEY_Device_FriendlyName", "{83DA6326-97A6-4088-9453-A1923F573B29} 10" }, null };

                        mo.InvokeMethod("GetDeviceProperties", argsd);
                        var mbos = (ManagementBaseObject[])argsd[1]; // one mbo for each device property key
                        var name = mbos[0].Properties.OfType<PropertyData>().FirstOrDefault().Value;
                        if (name != null)
                        {
                            if (name.ToString().Contains(Global.Config.GoProNetworkPrefix))
                            {
                                var parent = mbos[1].Properties.OfType<PropertyData>().FirstOrDefault().Value;

                                // для всех компонент            MessageBox.Show("NAME: " + name + " PARENT: " + parent);

                                if (!serials.Contains(parent))
                                {
                                    serials.Add(parent.ToString());

                                    MessageBox.Show("NAME: " + name + " PARENT: " + parent);
                                }
                            }

                        }
                    }
                      
                
         //   MessageBox.Show("endtimer");
            
            
            Connecting = false;
        }
        
    }

    static class Logger
    {
        // Запись последовательно без форматирования
        public static void Write(string text)
        {
            using (StreamWriter sw = new StreamWriter(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\log.txt", true))
            {
                sw.Write(text);
            }
        }

        // Запись строки с указанием времени
        public static void WriteLine(string message)
        {
            using (StreamWriter sw = new StreamWriter(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\log.txt", true))
            {
                sw.WriteLine(String.Format("{0,-23} {1}", DateTime.Now.ToString() + ":", message));
            }
        }
    }

    static class Global
    {
        public static XMLConfig Config;
    }

    // Структура для хранения конфигурации программы
    public struct XMLConfig
    {
        public string DestPath;                   // Папка назначения для копирования
        public bool MakeErrorLogs;                // Ведение логов ошибок
        public bool MakeWorkLogs;                 // Ведение логов копирования 
        public bool DeleteAfterCopy;              // Удалять файлы после копирования
        public string GoProNetworkPrefix;         // Часть наименования сетевого адаптера GoPro для поиска
    }




}
