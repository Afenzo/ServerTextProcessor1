using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text;
using System.Linq;
using System.Configuration;

namespace ServerTextProcessor1
{
    class Program
    {
        static string path;
        static string pathDB;
        static string address = "127.0.0.1";
        static int port;
        static TcpListener listener;
        public static ManualResetEvent sync,sync1;
        static void Main(string[] args)
        {
            if (args.Length > 1) //получаем адрес БД и порт
            {
                pathDB = args[0];
                port = Convert.ToInt32(args[1]);
            }
            else
            {
                Console.WriteLine("При запуске программы указаны не все параметры!");
                Console.ReadLine();
                return;
            }
            try
            {
                listener = new TcpListener(IPAddress.Parse(address), port);
                listener.Start();
                sync = new ManualResetEvent(true);
                sync1 = new ManualResetEvent(true);
                Thread commandThread = new Thread(CommandProcessing);
                commandThread.Start();
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ClientObject clientObject = new ClientObject(client);

                    // создаем новый поток для обслуживания нового клиента
                    Thread clientThread = new Thread(new ThreadStart(()=>ClientProcessing(client)));

                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (listener != null)
                    listener.Stop();
            }
        }
        static void CreateDictionary()
        {
            using (FrequencyContext db = new FrequencyContext())
            {
                string sqlQuery1 = "select * from Frequencies";
                var countFreq = db.Database.SqlQuery<Frequency>(sqlQuery1);
                if (countFreq.Count() == 0)
                {
                    int count = 1;
                    string[] separators = { ",", ".", "!", "?", ";", ":", " " };
                    try
                    {
                        string[] words = ReadAndSort(path);
                        for (int i = 0; i < words.Length - 1; i++)
                        {
                            if (words[i] == words[i + 1])
                            {
                                count++;
                            }
                            else
                            {
                                if (count >= 3 && words[i].Length >= 3) db.Frequencies.Add(new Frequency { Word = words[i], Amount = count });
                                count = 1;
                            }
                        }
                        db.SaveChanges();
                        Console.WriteLine("Создание завершено!");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
                else
                {
                    Console.WriteLine("В словаре уже есть записи. Вы можете очистить словарь, либо обновить его.");
                }
            }
        }
        static void CommandProcessing()
        {

            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.ConnectionStrings.ConnectionStrings.Remove("DBConnection");
            config.ConnectionStrings.ConnectionStrings.Add(
                new ConnectionStringSettings("DBConnection",
                "Data Source=(LocalDB)\\v12.0;AttachDbFilename="+pathDB+";Integrated Security=True", "System.Data.SqlClient"));
            config.Save(ConfigurationSaveMode.Full);

            Console.WriteLine("Параметры сервера:");
            Console.WriteLine("IP-адрес: "+address);
            Console.WriteLine("Порт: "+port);
            Console.WriteLine("");
            Console.WriteLine("Для работы со словарем вы можете ввести следующие команды:");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("создание [путь к текстовому файлу]");
            Console.WriteLine("обновление [путь к текстовому файлу]");
            Console.WriteLine("очистка словаря");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("Внимание! Путь к файлу должен иметь вид C:\\Users\\User1\\Documents\\Text.txt");
            Console.WriteLine("Путь вводится без скобок, отделяется от первого слова пробелом");
            string command;
            while (true)
            {
                command = Console.ReadLine();
                string[] arrCommand = command.Split(' ');
                if (arrCommand.Length != 2)
                {
                    Console.WriteLine("Введена неправильная команда!");
                }
                else
                {
                    try
                    {
                        arrCommand[0] = arrCommand[0].ToLower();
                        if (arrCommand[0] == "создание")
                        {
                            path = arrCommand[1];
                            sync1.WaitOne();
                            sync.Reset();
                            CreateDictionary();
                            sync.Set();
                        }
                        else if (arrCommand[0] == "обновление")
                        {
                            path = arrCommand[1];
                            sync1.WaitOne();
                            sync.Reset();
                            UpdateDictionary();
                            sync.Set();
                        }
                        else if ((arrCommand[0] + arrCommand[1]) == "очисткасловаря")
                        {
                            sync1.WaitOne();
                            sync.Reset();
                            ClearDictionary();
                            sync.Set();
                        }
                        else
                        {
                            Console.WriteLine("Введена неправильная команда!");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }                    
            }
        }
        static void ClientProcessing(TcpClient tcpClient)
        { 
            NetworkStream stream = null;
            TcpClient client = tcpClient;
            try
            {
                stream = client.GetStream();
                byte[] data = new byte[64]; // буфер для получаемых данных
                while (true)
                {
                    // получаем сообщение
                    StringBuilder builder = new StringBuilder();
                    int bytes = 0;
                    do
                    {
                        bytes = stream.Read(data, 0, data.Length);
                        builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                    }
                    while (stream.DataAvailable);

                    string command = builder.ToString();
                    string result = "";
                    System.Data.SqlClient.SqlParameter param = new System.Data.SqlClient.SqlParameter("@word", command + "%");
                    string sqlQuery1 = "select top 5 * from Frequencies where Word like @word order by Amount desc, Word desc";
                    sync.WaitOne(); //ожидаем окончания работы главного потока с бд
                    sync1.Reset(); //ограничиваем изменение данных на время чтения
                    using (FrequencyContext db = new FrequencyContext())
                    {
                        var frequency1 = db.Database.SqlQuery<Frequency>(sqlQuery1, param);

                        foreach (var item in frequency1)
                        {
                            result = result + item.Word + " ";
                        }
                    }
                    sync1.Set(); //разрешаем изменение данных
                    data = Encoding.Unicode.GetBytes(result);
                    stream.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (stream != null)
                    stream.Close();

                if (client != null)
                    client.Close();
            }
        }
        static string[] ReadAndSort(string path)
        {
            string text = "";
            string[] separators = { ",", ".", "!", "?", ";", ":", " ", "(", ")", "\"" };
            Console.WriteLine("Файл считывается");
            try
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    text = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            text = text.ToLower();
            string[] words = text.Split(separators, StringSplitOptions.RemoveEmptyEntries); //разбиваем на слова
            Array.Sort(words); //сортируем
            return words;
        }
        static void UpdateDictionary()
        {
            using (FrequencyContext db = new FrequencyContext())
            {
                try
                {
                    string[] newWords = ReadAndSort(path);
                    int i = 0, j = 0, count = 1, laterId = 0;
                    var oldFreq = db.Frequencies.OrderBy(f => f.Word);
                    int countOldFreq = oldFreq.Count();
                    bool found;

                    for (i = 0; i < newWords.Length - 1; i++)
                    {
                        if (newWords[i] == newWords[i + 1])
                        {
                            count++; //считаем повторения
                        }
                        else
                        {
                            found = false;
                            for (j = laterId; j < countOldFreq; j++)
                            {
                                if (oldFreq.AsEnumerable().ElementAt(j).Word == newWords[i]) //если слово из нового текста нашлось в бд
                                {
                                    oldFreq.AsEnumerable().ElementAt(j).Amount += count;
                                    //оба набора данных отсортированы, поэтому при новой итерации сравнения
                                    //можно не смотреть на пройденные элементы
                                    //сохраняем номер последнего совпавшего элемента
                                    //и следующие итерации начинаем с него, пока не обнаружим новое совпадение
                                    laterId = j;
                                    found = true;
                                    break;
                                }
                            }
                            if ((found == false)
                                && (count >= 3)
                                && (newWords[i].Length >= 3))
                            {   //если не нашли совпадения с бд - добавляем новый элемент
                                db.Frequencies.Add(new Frequency { Word = newWords[i], Amount = count });
                            }
                            count = 1;
                        }
                    }
                    db.SaveChanges();
                    if(i>0) Console.WriteLine("Обновление завершено!");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.ReadLine();
                    return;
                }
            }
        }
        static void ClearDictionary()
        {
            using (FrequencyContext db = new FrequencyContext())
            {
                db.Database.ExecuteSqlCommand("DELETE FROM Frequencies");
                Console.WriteLine("Очистка завершена!");
            }

        }
    }
}
