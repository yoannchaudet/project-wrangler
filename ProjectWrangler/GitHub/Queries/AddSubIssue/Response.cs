using System.Text.Json.Serialization;

namespace ProjectWrangler.GitHub.Queries.AddSubIssue;

public record Response(
    [property: JsonPropertyName("data")] Data? Data
);

public record Data(
    [property: JsonPropertyName("addSubIssue")]
    AddSubIssue? AddSubIssue
);

public record AddSubIssue(
    [property: JsonPropertyName("subIssue")]
    SubIssue SubIssue,
    [property: JsonPropertyName("clientMutationId")]
    string ClientMutationId
);

public record SubIssue(
    [property: JsonPropertyName("id")] string Id
);