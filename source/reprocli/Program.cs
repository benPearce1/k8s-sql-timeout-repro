using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace reprocli
{
    class Program
    {
        static void Main(string[] args)
        {
            var count = int.TryParse(args[0], out int c) ? c : 100;
            var connString = args[1];

            Task[] tasks = new Task[count];
            for (int i = 0; i < count; i++)
            {
                tasks[i] = CreateConnectionAndExecuteQuery(i, connString);
            }

            Task.WaitAll(tasks);
        }
        
        static async Task CreateConnectionAndExecuteQuery(int i, string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var tx = connection.BeginTransaction($"Connection-{i.ToString()}"))
                {
                    using (SqlCommand command = new SqlCommand("select * from Deployment where EnvironmentId = 'Environments-1'", connection, tx))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (reader.Read())
                            {
                                for (int j = 0; j < reader.FieldCount; j++)
                                {
                                    var value = reader.GetValue(j);
                                }
                            }
                        }
                    }

                    Thread.Sleep(5000);
                    tx.Commit();
                }
            }
        }
    }
}
