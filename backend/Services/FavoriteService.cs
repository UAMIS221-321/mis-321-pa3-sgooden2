using Dapper;
using MovieNight.Models;

namespace MovieNight.Services;

public class FavoriteService : BaseService
{
    public FavoriteService(IConfiguration configuration) : base(configuration) { }

    public async Task<List<Favorite>> GetAllAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<Favorite>(
            @"SELECT f.favorite_id, f.movie_id, f.title, f.added_at, m.genre, m.year
              FROM Favorites f
              JOIN Movies m ON f.movie_id = m.movie_id
              ORDER BY f.added_at DESC")).ToList();
    }

    public async Task<string> SaveAsync(int movieId, string title)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Favorites WHERE movie_id = @MovieId",
            new { MovieId = movieId }) > 0;

        if (exists)
            return $"\"{title}\" is already in your favorites!";

        await conn.ExecuteAsync(
            "INSERT INTO Favorites (movie_id, title) VALUES (@MovieId, @Title)",
            new { MovieId = movieId, Title = title });

        return $"Added \"{title}\" to your favorites!";
    }

    public async Task<string> RemoveAsync(int movieId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var title = await conn.ExecuteScalarAsync<string>(
            "SELECT title FROM Favorites WHERE movie_id = @MovieId",
            new { MovieId = movieId });

        if (title == null)
            return "That movie wasn't in your favorites.";

        await conn.ExecuteAsync(
            "DELETE FROM Favorites WHERE movie_id = @MovieId",
            new { MovieId = movieId });

        return $"Removed \"{title}\" from your favorites.";
    }
}
