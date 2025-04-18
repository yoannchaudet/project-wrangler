using System.Text.Json.Serialization;

namespace ProjectWrangler.GitHub.Queries.ProjectIssues;

public record Response(
    [property: JsonPropertyName("data")] Data Data
);

public record Data(
    [property: JsonPropertyName("organization")]
    Organization? Organization
);

public record Organization(
    [property: JsonPropertyName("projectV2")]
    ProjectV2 ProjectV2
);

public record ProjectV2(
    [property: JsonPropertyName("items")] ProjectV2Items Items
);

public record ProjectV2Items(
    [property: JsonPropertyName("pageInfo")]
    PageInfo PageInfo,
    [property: JsonPropertyName("nodes")] List<Node> Nodes
);

public record PageInfo(
    [property: JsonPropertyName("hasNextPage")]
    bool HasNextPage,
    [property: JsonPropertyName("endCursor")]
    string EndCursor
);

public record Node(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content")]
    Content Content, // should it be nullable?
    [property: JsonPropertyName("fieldValueByName")]
    FieldValueByName? FieldValueByName
);

public record Content(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("parent")] Parent? Parent
);

public record Parent(
    [property: JsonPropertyName("id")] string Id
);

public record FieldValueByName(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("optionId")]
    string OptionId
);