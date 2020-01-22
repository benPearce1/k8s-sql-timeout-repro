using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace reprocli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var count = int.TryParse(args[0], out int c) ? c : 100;
            var connString = args[1];

            
            
            Task[] tasks = new Task[count];
            for (int i = 0; i < count; i++)
            {
                StringBuilder sb = new StringBuilder("CREATE TABLE #temptbl(ID int IDENTITY(1,1) PRIMARY KEY, value varchar(max))").AppendLine("GO");
                for (int j = 0; j < 3; j++)
                {
                    sb.AppendLine($"INSERT INTO #temptbl(test) Values('{(string.Join(" ", Enumerable.Range(0, 200).Select(x => Guid.NewGuid())))}')").AppendLine("GO");
                }
                sb.AppendLine("SELECT * FROM #temptbl").AppendLine("GO");
                sb.AppendLine("DROP TABLE #temptbl").AppendLine("GO");
                Console.WriteLine(sb.ToString());
                tasks[i] = CreateConnectionAndExecuteCommand(i, connString, sb.ToString());
            }

            Task.WaitAll(tasks);
            
            await CreateConnectionAndExecuteCommand(-1, connString, "DROP TABLE #temptbl");
        }
        
        static async Task CreateConnectionAndExecuteCommand(int i, string connectionString, string query, Func<SqlCommand, Task> callback = null)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                Console.WriteLine($"{DateTime.Now} - Started connection {i}");
                using (var tx = connection.BeginTransaction($"Connection-{i.ToString()}"))
                {
                    using (SqlCommand command = new SqlCommand(query, connection, tx))
                    {
                        if (callback != null)
                        {
                            await callback(command);
                        }
                    }

                    Thread.Sleep(5000);
                    tx.Commit();
                }
            }
            
            Console.WriteLine($"{DateTime.Now} - Finshing connection {i}");
        }
        
        static async Task ReadFields(SqlCommand command)
        {
            int length = 0;
            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                while (reader.Read())
                {
                    for (int j = 0; j < reader.FieldCount; j++)
                    {
                        var value = reader.GetString(j);
                        length += value.Length;
                    }
                }
            }
            Console.WriteLine($"Read {length} characters from database");
        }
    }
}
