using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase {
        private readonly IConfiguration configuration;

        public TestController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        [HttpGet("{count}")]
        public async Task<ActionResult<string[]>> Run(int count)
        {
            List<string> output = new List<string>();
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < count; i++)
            {
                tasks.Add(CreateConnection(i));
            }

            var connection = new SqlConnection(configuration.GetConnectionString("db"));
            await connection.OpenAsync();

            while (tasks.Any(t => !t.IsCompleted))
            {
                using (var tx = connection.BeginTransaction(Thread.CurrentThread.Name))
                {
                    SqlCommand command = connection.CreateCommand();
                    command.CommandText = $"DBCC OPENTRAN [{connection.Database}]";
                    command.Transaction = tx;
                    var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult);
                    while (reader.HasRows)
                    {
                        while (await reader.ReadAsync())
                        {
                            output.Add(reader.GetString(1));
                        }
                        
                        await reader.NextResultAsync();
                    }

                    await reader.CloseAsync();
                }
                
                Thread.Sleep(100);
            }

            connection.Close();
            return output.ToArray();
        }

        async Task CreateConnection(int i)
        {
            var connection = new SqlConnection(configuration.GetConnectionString("db"));
            await connection.OpenAsync();

            using (var tx = connection.BeginTransaction($"Thread-{i.ToString()}"))
            {
                Console.WriteLine(i);
                Thread.Sleep(5000);
                tx.Commit();
            }

            connection.Close();
        }
    }
}
