# k8s-sql-timeout-repro

To run test: 

`dotnet run <number of parallel connections to create> <sql connection string>`

Example

`dotnet run 10 'server=.;inital catalog=db;trusted_connection=true'`

Compile

I have been including a self-contained version in the git repo as our instances only have the runtime.
`dotnet publish -r linux-x64 --self-contained -o .\compiled\linux-x64`

Download into the container and run main test

```
cd ~
apt install git
git clone https://github.com/benPearce1/k8s-sql-timeout-repro
git checkout tiny
cd ./k8s-sql-timeout-repro/source/reprocli/compiled/linux-x64
dotnet reprocli.dll <connection count> '<sql connection string>'
```
^ In linux, the sql connection string may need to be single-quoted due to characters in the password

For the second repro, it is run via a web api app.

```
apt install wget
cd ./k8s-sql-timeout-repro/source/compiled/linux-x64
dotnet webapi.dll
```
In second bash session:
Create file with below contents, modifying the number of calls and the port

```
#!/bin/bash
for number in {1..20}
do
wget http://localhost:22354/api/values/sqlnet &
done 
exit 0
wait
```
