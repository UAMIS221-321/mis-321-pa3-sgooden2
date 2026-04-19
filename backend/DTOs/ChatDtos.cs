namespace MovieNight.DTOs;

public class ChatRequest
{
    public string Message { get; set; } = "";
    public List<HistoryMessage> History { get; set; } = new();
}

public class HistoryMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

public class ChatResponse
{
    public string Reply { get; set; } = "";
}
