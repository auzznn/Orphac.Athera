namespace Orphac.Athera.Api.Endpoints;

internal static class ProblemResults
{
    public static IResult Create(
        HttpContext context,
        int statusCode,
        string errorCode,
        string title,
        string detail)
    {
        return Results.Problem(
            detail: detail,
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = errorCode,
                ["traceId"] = context.TraceIdentifier
            });
    }
}
