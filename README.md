# k8s-sql-timeout-repro

To run test: 

`dotnet run <number of parallel connections to create> <sql connection string>`

Example

`dotnet run 10 'server=.;inital catalog=db;trusted_connection=true`
