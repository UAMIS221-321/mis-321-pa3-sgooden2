using Dapper;
using MovieNight.Models;

namespace MovieNight.Services;

/// <summary>
/// RAG (Retrieval-Augmented Generation) service.
/// Searches the Movies table for entries relevant to the user's message
/// and builds a context string that is injected into the LLM system prompt.
/// </summary>
public class RagService : BaseService
{
    // Words too generic to be useful as search keywords
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a","an","the","is","are","was","were","be","been","being",
        "have","has","had","do","does","did","will","would","could","should","may","might","must",
        "and","or","but","not","so","yet","for","nor",
        "in","on","at","to","of","with","by","from","about","into","through","during",
        "this","that","these","those","it","its",
        "what","which","who","whom","whose","when","where","why","how",
        "i","me","my","you","your","we","our","they","their","he","she","him","her",
        "want","need","looking","recommend","tell","show","give","find","get","see","know","like","love",
        "good","great","best","top","popular","famous","watch","watching",
        "movie","movies","film","films","show","shows"
    };

    public RagService(IConfiguration configuration) : base(configuration) { }

    /// <summary>
    /// Extracts keywords from the user's message and retrieves the most relevant movies
    /// from the database using a scored LIKE search across all metadata fields.
    /// </summary>
    public async Task<List<Movie>> RetrieveRelevantMoviesAsync(string userMessage)
    {
        var keywords = userMessage
            .ToLower()
            .Split(new[] { ' ', ',', '.', '?', '!', '\'', '"', '(', ')', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !StopWords.Contains(w))
            .Distinct()
            .Take(8)
            .ToList();

        using var conn = CreateConnection();
        await conn.OpenAsync();

        if (!keywords.Any())
        {
            // No useful keywords — return top-rated movies as default context
            return (await conn.QueryAsync<Movie>(
                "SELECT * FROM Movies ORDER BY rating DESC LIMIT 5")).ToList();
        }

        // Score each movie by how many keywords appear in its metadata.
        // Movies with higher scores (more keyword matches) rank first.
        var scoreParts = keywords.Select((_, i) =>
            $"(CASE WHEN LOWER(title) LIKE @k{i} OR LOWER(genre) LIKE @k{i} " +
            $"OR LOWER(director) LIKE @k{i} OR LOWER(cast_members) LIKE @k{i} " +
            $"OR LOWER(plot) LIKE @k{i} THEN 1 ELSE 0 END)").ToList();

        var sql = $"SELECT *, ({string.Join(" + ", scoreParts)}) AS score " +
                  "FROM Movies HAVING score > 0 ORDER BY score DESC, rating DESC LIMIT 5";

        var parameters = new DynamicParameters();
        for (int i = 0; i < keywords.Count; i++)
            parameters.Add($"k{i}", $"%{keywords[i]}%");

        var results = (await conn.QueryAsync<Movie>(sql, parameters)).ToList();

        // Fallback: if nothing matched, return top-rated movies so the LLM still has context
        if (!results.Any())
            results = (await conn.QueryAsync<Movie>(
                "SELECT * FROM Movies ORDER BY rating DESC LIMIT 5")).ToList();

        return results;
    }

    /// <summary>
    /// Formats the retrieved movies into a readable context string for the LLM prompt.
    /// Includes the movie_id so the LLM can reference it in function calls.
    /// </summary>
    public string BuildContext(List<Movie> movies)
    {
        if (!movies.Any()) return "No movies found in the database.";

        var sb = new System.Text.StringBuilder();
        foreach (var m in movies)
        {
            sb.AppendLine($"Movie ID: {m.MovieId}");
            sb.AppendLine($"Title: {m.Title} ({m.Year})");
            sb.AppendLine($"Genre: {m.Genre}");
            sb.AppendLine($"Director: {m.Director}");
            sb.AppendLine($"Cast: {m.CastMembers}");
            sb.AppendLine($"Rating: {m.Rating}/10");
            sb.AppendLine($"Plot: {m.Plot}");
            sb.AppendLine("---");
        }
        return sb.ToString();
    }
}
