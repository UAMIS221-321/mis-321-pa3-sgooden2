using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MovieNight.DTOs;

namespace MovieNight.Services;

/// <summary>
/// Handles all communication with the Anthropic API.
/// Implements Function Calling (tool use): the LLM can invoke save_to_favorites,
/// remove_from_favorites, and get_favorites, which execute real database operations.
/// </summary>
public class AnthropicService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;
    private readonly FavoriteService _favoriteService;

    private const string Model = "claude-haiku-4-5-20251001";
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    public AnthropicService(IConfiguration configuration, FavoriteService favoriteService, IHttpClientFactory httpClientFactory)
    {
        // Support both appsettings.json and Heroku environment variable
        _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? configuration["Anthropic:ApiKey"]
            ?? throw new InvalidOperationException("Anthropic API key not configured.");
        _favoriteService = favoriteService;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Main entry point. Injects the RAG context into the system prompt,
    /// sends the conversation to Claude, and handles any tool calls in a loop.
    /// </summary>
    public async Task<string> ChatAsync(string ragContext, List<HistoryMessage> history, string userMessage)
    {
        var systemPrompt =
            "You are Movie Night, an enthusiastic AI movie recommendation assistant!\n" +
            "Help users discover films, discuss movies, and manage their favorites list.\n\n" +
            "IMPORTANT: The movie database context below contains Movie IDs. " +
            "Always use the exact movie_id from the context when calling save_to_favorites or remove_from_favorites.\n\n" +
            "Tools available:\n" +
            "- save_to_favorites: Add a movie to the user's favorites (needs movie_id + movie_title)\n" +
            "- remove_from_favorites: Remove a movie from favorites (needs movie_id)\n" +
            "- get_favorites: Show the user's current favorites list\n\n" +
            "Be enthusiastic, fun, and knowledgeable. Recommend specific movies from the context when relevant.\n\n" +
            "RELEVANT MOVIES FROM DATABASE (retrieved via RAG):\n" +
            ragContext;

        // Build messages array from conversation history + new user message
        var messages = new JsonArray();
        foreach (var h in history)
            messages.Add(MakeTextMessage(h.Role, h.Content));
        messages.Add(MakeTextMessage("user", userMessage));

        return await RunToolLoopAsync(systemPrompt, messages);
    }

    /// <summary>
    /// Calls the Anthropic API and loops until the model produces a final text response.
    /// Each iteration either returns text (done) or executes tool calls and continues.
    /// </summary>
    private async Task<string> RunToolLoopAsync(string systemPrompt, JsonArray messages)
    {
        var toolsJson = JsonSerializer.Serialize(BuildTools());
        var http = _httpClientFactory.CreateClient();

        for (int iteration = 0; iteration < 5; iteration++)
        {
            // Build request body
            var requestBody =
                "{\"model\":" + JsonSerializer.Serialize(Model) +
                ",\"max_tokens\":1024" +
                ",\"system\":" + JsonSerializer.Serialize(systemPrompt) +
                ",\"tools\":" + toolsJson +
                ",\"messages\":" + messages.ToJsonString() + "}";

            var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Anthropic API error {response.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var stopReason = root.GetProperty("stop_reason").GetString();
            var contentArray = root.GetProperty("content");
            var contentRaw = contentArray.GetRawText();

            // No tool calls — return the text response
            if (stopReason != "tool_use")
            {
                foreach (var block in contentArray.EnumerateArray())
                    if (block.GetProperty("type").GetString() == "text")
                        return block.GetProperty("text").GetString() ?? "";
                return "I couldn't generate a response. Please try again!";
            }

            // Append the assistant's message (which contains tool_use blocks) to history
            messages.Add(JsonNode.Parse(
                "{\"role\":\"assistant\",\"content\":" + contentRaw + "}")!);

            // Execute each tool call and collect results
            var toolResults = new List<string>();
            foreach (var block in contentArray.EnumerateArray())
            {
                if (block.GetProperty("type").GetString() != "tool_use") continue;

                var toolUseId = block.GetProperty("id").GetString()!;
                var toolName = block.GetProperty("name").GetString()!;
                var toolInput = block.GetProperty("input");

                var result = await ExecuteToolAsync(toolName, toolInput);

                toolResults.Add(
                    "{\"type\":\"tool_result\"" +
                    ",\"tool_use_id\":" + JsonSerializer.Serialize(toolUseId) +
                    ",\"content\":" + JsonSerializer.Serialize(result) + "}");
            }

            // Append tool results as a user message so Claude sees the outcomes
            messages.Add(JsonNode.Parse(
                "{\"role\":\"user\",\"content\":[" + string.Join(",", toolResults) + "]}")!);
        }

        return "I had trouble processing that request. Please try again!";
    }

    /// <summary>
    /// Dispatches a tool call to the appropriate service method and returns a result string.
    /// </summary>
    private async Task<string> ExecuteToolAsync(string toolName, JsonElement input)
    {
        return toolName switch
        {
            "save_to_favorites" => await _favoriteService.SaveAsync(
                input.GetProperty("movie_id").GetInt32(),
                input.GetProperty("movie_title").GetString() ?? ""),

            "remove_from_favorites" => await _favoriteService.RemoveByTitleAsync(
                input.GetProperty("movie_title").GetString() ?? ""),

            "get_favorites" => await GetFavoritesAsTextAsync(),

            _ => $"Unknown tool: {toolName}"
        };
    }

    private async Task<string> GetFavoritesAsTextAsync()
    {
        var favs = await _favoriteService.GetAllAsync();
        if (!favs.Any())
            return "Your favorites list is empty. Ask me for recommendations!";

        var lines = favs.Select(f => $"- {f.Title} ({f.Year}) | {f.Genre} | Movie ID: {f.MovieId}");
        return "Your favorites:\n" + string.Join("\n", lines);
    }

    // --- Tool definitions sent to Anthropic ---

    private static object[] BuildTools() => new[]
    {
        (object)new
        {
            name = "save_to_favorites",
            description = "Save a movie to the user's favorites list in the database",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    movie_id    = new { type = "integer", description = "The database ID of the movie (from the RAG context)" },
                    movie_title = new { type = "string",  description = "The title of the movie" }
                },
                required = new[] { "movie_id", "movie_title" }
            }
        },
        new
        {
            name = "remove_from_favorites",
            description = "Remove a movie from the user's favorites list by title",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    movie_title = new { type = "string", description = "The title of the movie to remove" }
                },
                required = new[] { "movie_title" }
            }
        },
        new
        {
            name = "get_favorites",
            description = "Retrieve the user's current favorites list from the database",
            input_schema = new
            {
                type = "object",
                properties = new { }
            }
        }
    };

    private static JsonNode MakeTextMessage(string role, string content) =>
        JsonNode.Parse(
            "{\"role\":" + JsonSerializer.Serialize(role) +
            ",\"content\":" + JsonSerializer.Serialize(content) + "}")!;
}
