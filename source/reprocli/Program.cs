using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Data.SqlClient;

namespace reprocli
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var count = int.TryParse(args[0], out int c) ? c : 100;
                var connString = args[1];

                var total = Stopwatch.StartNew();

               /* Enumerable.Range(0, count)
                            .AsParallel()
                            .WithDegreeOfParallelism(10)
                            .ForAll(number => Scenario2(connString, number));

                total.Stop();
                Console.WriteLine($"Scenario 2 Total: {total.Elapsed}");

                total.Restart();
                Enumerable.Range(0,count)
                    .AsParallel()
                    .WithDegreeOfParallelism(10)
                    .ForAll(n => Scenario3(connString, n));
                Console.WriteLine($"Scenario 3 total: {total.Elapsed}"); */


                var queries = GetQueries();
                total.Restart();
                Enumerable.Range(0,count)
                    .AsParallel()
                    .WithDegreeOfParallelism(count)
                    .ForAll(n => Scenario4(connString, n, queries));
                Console.WriteLine($"Scenario 4 total (simulate 1 deployment): {total.Elapsed}");


                //await Scenario1(connString, count);
                //Scenario2(connString, count);


            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }


        static string[] GetQueries()
        {
            var document = new XmlDocument();
            document.PreserveWhitespace = true;
            document.Load(@"SQL.xml");
            var queries = new List<string>();

            var nodes = document.GetElementsByTagName("Event");

            foreach(var node in nodes.Cast<XmlNode>())
            {
                var children = node.ChildNodes.Cast<XmlNode>().ToArray();
                var sql = children.SingleOrDefault(chnode => chnode.Name == "Column" && chnode.Attributes.Cast<XmlAttribute>().Any(at => at.Name == "name" && at.Value == "TextData"));

                if (sql != null)
                {
                    if (!sql.InnerText.StartsWith("exec sp_reset_connection") &&
                        !sql.InnerText.StartsWith("-- network protocol: TCP/IP") &&
                        !sql.InnerText.Contains("INSERT") &&
                        !sql.InnerText.Contains("UPDATE") &&
                        !sql.InnerText.Contains("DELETE"))

                    {
                        queries.Add(sql.InnerText);
                    }
                }
            }

            return queries.ToArray();
        }

        private static void Scenario4(string connString, int number, string[] queries)
        {
            var totalTenantStopWatch = Stopwatch.StartNew();

            var buffer = new object[100];
            for (var i = 0; i < queries.Length; i++)
            {
                var queryStopWatch = Stopwatch.StartNew();

                var query = queries[i];
                using (var connection = new SqlConnection(connString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        using (var command = new SqlCommand(query, connection, transaction))
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

            totalTenantStopWatch.Stop();
            Console.WriteLine($"Number: {number}. All Queries. Time: {totalTenantStopWatch.Elapsed}");
        }


        private static void Scenario3(string connString, int n)
        {
            SqlDataReader reader2 = null;
            string query1 =
                @"Select [Id]
                ,[OwnerId]
                ,[Version]
                ,[IsFrozen]
                ,[JSON]
                ,[RelatedDocumentIds]
                ,[SpaceId]
                ,[OwnerType] From VariableSet Where OwnerId = 'Deployments-2809'";
            string query2 = "SELECT Id From [VariableSet] Where OwnerId = 'Deployments-2806'";

            using (SqlConnection awConnection = new SqlConnection(connString))
            {
                SqlCommand cmd1 = new SqlCommand(query1, awConnection);
                SqlCommand cmd2 =
                    new SqlCommand(query2, awConnection);

                awConnection.Open();
                using (SqlDataReader reader1 = cmd1.ExecuteReader())
                {
                    while (reader1.Read())
                    {
                        Console.WriteLine(reader1["OwnerId"]);

                        // The following line of code requires
                        // a MARS-enabled connection.
                        reader2 = cmd2.ExecuteReader();
                        using (reader2)
                        {
                            while (reader2.Read())
                            {
                                Console.WriteLine(reader2["Id"].ToString());
                            }
                        }
                    }
                }
            }
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
                        ,[OwnerType] From VariableSet Where OwnerId = 'Deployments-2800'", connection, transaction))
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
