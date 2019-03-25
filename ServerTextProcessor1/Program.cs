using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace ServerTextProcessor1
{
    class Program
    {
        const int port = 8888;
        static TcpListener listener;
        public static ManualResetEvent sync;
        static void Main(string[] args)
        {
            try
            {
                listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
                listener.Start();
                Console.WriteLine("Ожидание подключений...");
                sync = new ManualResetEvent(false);
                Thread commandThread = new Thread(CommandProcessing);
                commandThread.Start();
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ClientObject clientObject = new ClientObject(client);
                    
                    // создаем новый поток для обслуживания нового клиента
                    Thread clientThread = new Thread(new ThreadStart(clientObject.Process));
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
                string path = @"C:\Users\mindw\source\repos\TextProcessor1\TextProcessor1\Text.txt";
                string text;
                int count = 1;
                string[] separators = { ",", ".", "!", "?", ";", ":", " " };
                try
                {
                    Console.WriteLine("******считываем весь файл********");
                    using (StreamReader sr = new StreamReader(path))
                    {
                        text = sr.ReadToEnd();
                    }
                    string[] words = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                    Array.Sort(words);

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
                    Console.WriteLine(words.Length);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
        static void CommandProcessing()
        {
            string command;
            while (true)
            {
                command = Console.ReadLine();
                string[] arrCommand = command.Split(' ');
                if (arrCommand.Length < 2)
                {
                    Console.WriteLine("Введена неправильная команда!");
                }
                else
                {
                    arrCommand[0] = arrCommand[0].ToLower();
                    if (arrCommand[0] == "создание")
                    {
                        CreateDictionary();
                    }
                    else if (arrCommand[0] == "обновление")
                    {

                    }
                    else if ((arrCommand[0] + arrCommand[1]) == "очисткасловаря")
                    {

                    }
                    else
                    {
                        Console.WriteLine("Введена неправильная команда!");
                    }
                }                    
            }
        }
    }
}
