using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace reprocli
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var count = int.Parse(args[0]);
                var connectionString = args[1];

                PrepareData(connectionString);
                var sync = Stopwatch.StartNew();
                Enumerable.Range(0,count)
                    .AsParallel()
                    .WithDegreeOfParallelism(count)
                    .ForAll(n => Sync(connectionString, n));

                @sync.Stop();

                PrepareData(connectionString);
                var @async = Stopwatch.StartNew();
                var tasks = Enumerable.Range(0,count)
                    .Select(n => Async(connectionString, n))
                    .ToArray();

                Task.WaitAll(tasks);
                @async.Stop();

                Console.WriteLine($"Total Sync: {sync.Elapsed}");
                Console.WriteLine($"Total Async: {@async.Elapsed}");

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static void Sync(string connString, int number)
        {
            var userStopWatch = Stopwatch.StartNew();

            var buffer = new object[100];
            for (var i = 0; i < 210; i++)
            {
                var queryStopWatch = Stopwatch.StartNew();

                using (var connection = new SqlConnection(connString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        using (var command = new SqlCommand("SELECT * From TestTable", connection, transaction))
                        {
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    reader.GetValues(buffer);
                                }
                            }
                        }

                        transaction.Commit();
                    }
                }

                queryStopWatch.Stop();
                Console.WriteLine($"Number: {number}. Query: {i} Time: {queryStopWatch.Elapsed}");
            }

            userStopWatch.Stop();
            Console.WriteLine($"Number: {number}. All Queries. Time: {userStopWatch.Elapsed}");
        }

        private static async Task Async(string connString, int number)
        {
            var userStopWatch = Stopwatch.StartNew();

            var buffer = new object[100];
            for (var i = 0; i < 210; i++)
            {
                var queryStopWatch = Stopwatch.StartNew();


                using (var connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted))
                    {
                        using (var command = new SqlCommand("SELECT * From TestTable", connection, transaction))
                        {
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    reader.GetValues(buffer);
                                }
                            }
                        }

                        await transaction.CommitAsync();
                    }
                }

                queryStopWatch.Stop();
                Console.WriteLine($"Number: {number}. Query: {i} Time: {queryStopWatch.Elapsed}");
            }

            userStopWatch.Stop();
            Console.WriteLine($"Number: {number}. All Queries. Time: {userStopWatch.Elapsed}");
        }




        static void PrepareData(string connectionString)
        {
            var createTable = @"
                DROP TABLE IF EXISTS TestTable;
                CREATE TABLE TestTable
                (
                    [Id] [nvarchar](50) NOT NULL PRIMARY KEY,
                    [Name] [nvarchar](20) NOT NULL
                );";

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    using (var command = new SqlCommand(createTable, connection, transaction))
                    {
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }

        }
    }
}
