using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace reprocli
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting...");

                var count = int.Parse(args[0]);
                var option = args[1];
                var connectionString = args[2];
                var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);

                Console.WriteLine($"Running '{option}' with MARS set to '{connectionStringBuilder.MultipleActiveResultSets}'");

                PrepareData(connectionString);

                var watch = Stopwatch.StartNew();

                switch (option)
                {
                    case "sync":
                    {
                        Enumerable.Range(0,count)
                            .AsParallel()
                            .WithDegreeOfParallelism(count)
                            .ForAll(n => Sync(connectionString, n));
                        break;
                    }
                    case "async":
                    {
                        var tasks = Enumerable.Range(0,count)
                            .Select(n => Async(connectionString, n))
                            .ToArray();

                        Task.WaitAll(tasks);
                        break;
                    }
                    case "async2":
                    {
                        async IAsyncEnumerable<int> RangeAsync()
                        {
                            for (var i = 0; i < count; i++)
                            {
                                await Async(connectionString, i);
                                yield return i;
                            }
                        }
                        await foreach (var _ in RangeAsync())
                        { }

                        break;
                    }
                    default:
                    {
                        throw new Exception("sync, async and async2 are valida values for the second parameter");
                    }
                }

                watch.Stop();


                Console.WriteLine($"Total for '{option}' with MARS({connectionStringBuilder.MultipleActiveResultSets}): {watch.Elapsed}");
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
