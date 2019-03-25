using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace ServerTextProcessor1
{
    public class ClientObject
    {
        public TcpClient client;
        public ClientObject(TcpClient tcpClient)
        {
            client = tcpClient;
        }

        public void Process()
        {
            NetworkStream stream = null;
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
                    string result="";
                    System.Data.SqlClient.SqlParameter param = new System.Data.SqlClient.SqlParameter("@word", command + "%");
                    string sqlQuery1 = "select top 5 * from Frequencies where Word like @word order by Amount desc, Word desc";
                    using (FrequencyContext db = new FrequencyContext())
                    {
                        var frequency1 = db.Database.SqlQuery<Frequency>(sqlQuery1, param);

                        foreach (var item in frequency1)
                        {
                            Console.WriteLine(item);
                            result = result + item.Word + " ";
                        }
                    }
                    Console.WriteLine("Результат: "+result);
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
    }
}
