using OpenAIAPI_Rystem.Services;
using Shared;

namespace OpenAIAPI_Rystem.Functions;

public class MySqlQueryAPIFunction : FunctionBase<MySqlQueryRequest, MySqlResponse>
{
    public const string FUNCTION_NAME = "query_mysql";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Queries a MySQL database given a host, database, and SQL query.  Creds and host are optional and default to a developer account and localhost.  Don't fill them in unless explicilty specified.  Database is optional but has no default (maybe useful for getting schema info).  Be very careful to properly escape your queries.";
    
    private readonly IMySqlService _mySqlService;

    public MySqlQueryAPIFunction(IMySqlService mySqlService, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _mySqlService = mySqlService ?? throw new ArgumentNullException(nameof(mySqlService));
    }

    protected override async Task<MySqlResponse> ExecuteFunctionAsync(MySqlQueryRequest request)
    {
        return await _mySqlService.Query(request);
    }
}

public class MySqlNonQueryAPIFunction : FunctionBase<MySqlQueryRequest, MySqlResponse>
{
    public const string FUNCTION_NAME = "nonquery_mysql";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Executes a non-query SQL command (e.g., INSERT, UPDATE, DELETE) on a MySQL database given a host, database, and SQL query.  Creds and host are optional and default to a developer account and localhost.  Don't fill them in unless explicilty specified.  Database is optional but has no default (maybe useful for getting schema info).  Be very careful to properly escape your queries.";
    
    private readonly IMySqlService _mySqlService;

    public MySqlNonQueryAPIFunction(IMySqlService mySqlService, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _mySqlService = mySqlService ?? throw new ArgumentNullException(nameof(mySqlService));
    }

    protected override async Task<MySqlResponse> ExecuteFunctionAsync(MySqlQueryRequest request)
    {
        return await _mySqlService.NonQuery(request);
    }
}

