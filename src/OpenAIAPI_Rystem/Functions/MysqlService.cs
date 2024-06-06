using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace OpenAIAPI_Rystem.Functions;

public interface IMySqlService
{
    Task<MySqlResponse> Query(MySqlQueryRequest request);
    Task<MySqlResponse> NonQuery(MySqlQueryRequest request);
}

public class MySqlService : IMySqlService
{
    private MySqlConnection GetConnection(string host, string database, string username, string password)
    {
        string connectionString = $"Server={host};Database={database};User ID={username};Password={password};";
        return new MySqlConnection(connectionString);
    }

    public async Task<MySqlResponse> Query(MySqlQueryRequest request)
    {
        try
        {
            using var connection = GetConnection(request.Host, request.Database, request.Username, request.Password);
            
            await connection.OpenAsync();
            using var command = new MySqlCommand(request.Query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            var dataTable = new DataTable();
            dataTable.Load(reader);

            return new MySqlResponse() { Result = JsonConvert.SerializeObject(dataTable) };
        }
        catch (Exception ex)
        {
            return new MySqlResponse() { Error = ex.Message };
        }
    }

    public async Task<MySqlResponse> NonQuery(MySqlQueryRequest request)
    {
        try
        {
            using var connection = GetConnection(request.Host, request.Database, request.Username, request.Password);
            await connection.OpenAsync();
            using var command = new MySqlCommand(request.Query, connection);
            
            int rowsAffected = await command.ExecuteNonQueryAsync();
            return new MySqlResponse() { Result = $"Rows affected: {rowsAffected}" };
        }
        catch (Exception ex)
        {
            return new MySqlResponse() { Error = ex.Message };
        }
    }
}

public class MySqlQueryRequest
{
    public string Host { get; set; } = "localhost";
    public string Database { get; set; }
    public string Query { get; set; }
    public string Username { get; set; } = "developer";
    public string Password { get; set; } = "password";
}

public class MySqlResponse
{
    public string Result { get; set; }
    public string Error { get; set; }
}