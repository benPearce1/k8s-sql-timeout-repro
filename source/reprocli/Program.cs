using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

            var total = Stopwatch.StartNew();

            Enumerable.Range(0, count)
                        .AsParallel()
                        .WithDegreeOfParallelism(10)
                        .ForAll(number => Scenario2(connString, number));

            total.Stop();

            Console.WriteLine($"Total: {total.Elapsed}");

            //await Scenario1(connString, count);
            //Scenario2(connString, count);
        }

        private static void Scenario2(string connString, int number)
        {
            //for (var i = 0; i < count; i++)
            var stopwatch = Stopwatch.StartNew();
            {
                using (var connection = new SqlConnection(connString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        using (var command = new SqlCommand(@"Select [Id]
                            ,[OwnerId]
                        ,[Version]
                        ,[IsFrozen]
                        ,[JSON]
                        ,[RelatedDocumentIds]
                        ,[SpaceId]
                        ,[OwnerType] From VariableSet Where OwnerId = 'Deployments-1869'", connection, transaction))
                        {
                            using (var reader = command.ExecuteReader())
                            {
                                reader.Read();
                                Console.WriteLine($"I: {number}, ID: " + reader.GetString(0));
                            }
                        }

                        transaction.Commit();
                    }
                }
            }

            stopwatch.Stop();

            Console.WriteLine($"Number: {number}. Time: {stopwatch.Elapsed}");
        }


        private static async Task Scenario1(string connString, int count)
        {
            await CreateConnectionWithCallbackExecute(-1, connString,
                "if not exists (select * from sysobjects where name='temptbl' and xtype='U') CREATE TABLE temptbl(ID int IDENTITY(1,1) PRIMARY KEY,test varchar(max))",
                ExecuteNonQuery);

            Task[] tasks = new Task[count];
            for (int i = 0; i < count; i++)
            {
                StringBuilder sb = new StringBuilder();
                for (int j = 0; j < 5; j++)
                {
                    sb.AppendLine(
                        $"INSERT INTO temptbl(test) Values('{(string.Join(" ", Enumerable.Range(0, 200).Select(x => Guid.NewGuid())))}')");
                }

                tasks[i] = CreateConnectionWithCallbackExecute(i, connString, sb.ToString(), ExecuteNonQuery);
            }

            Task.WaitAll(tasks);

            await CreateConnectionWithCallbackExecute(-1, connString, "select test from temptbl", ReadFieldLength);

            await CreateConnectionWithCallbackExecute(-1, connString, $"DROP TABLE temptbl", ExecuteNonQuery);
        }

        static async Task CreateConnectionWithCallbackExecute(int i, string connectionString, string query, Func<SqlCommand, Task> execute)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                Console.WriteLine($"{DateTime.Now} - Opened connection {i}");
                using (var tx = connection.BeginTransaction($"Connection-{i.ToString()}"))
                {
                    using (SqlCommand command = new SqlCommand(query, connection, tx))
                    {
                        await execute(command);
                    }

                    Thread.Sleep(5000);
                    tx.Commit();
                }
            }

            Console.WriteLine($"{DateTime.Now} - Closed connection {i}");
        }

        static async Task ReadFieldLength(SqlCommand command)
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

        static async Task ExecuteNonQuery(SqlCommand command)
        {
            int count = await command.ExecuteNonQueryAsync();
            Console.WriteLine($"Records affected: {count}");
        }
    }
}
