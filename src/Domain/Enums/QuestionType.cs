using System.Text.Json.Serialization;

namespace backend.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QuestionType
{
    SingleChoice   = 0,
    MultipleChoice = 1,
    TextInput      = 2,
    Rating         = 3,
    LikertScale    = 4
}
