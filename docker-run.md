docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Aa123456" -p 1433:1433 --name=mysql_server -v sqlserverdata:/var/opt/mssql -d mcr.microsoft.com/mssql/server
