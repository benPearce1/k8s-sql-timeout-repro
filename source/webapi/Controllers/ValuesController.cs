using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;

namespace webapi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly IConfiguration config;

        public ValuesController(IConfiguration config) {
            this.config = config;
        }
        
        [HttpGet()]
        public async Task<IActionResult> Get() {

            var connString = config.GetConnectionString("Db");
            using (var connection = new SqlConnection(connString)) {
                try {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = "select * from sys.tables";
                    var reader = command.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                    var count = 0;
                    while(reader.Read())
                    {
                        count++;
                    }

                    return Ok(count);
                }
                catch(Exception ex)
                 {
                     throw new InvalidOperationException(ex.ToString());
                 }
            }
            return Ok();
        }
    }
}
