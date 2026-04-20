using Microsoft.AspNetCore.Mvc;
using MovieNight.Services;

namespace MovieNight.Controllers;

[ApiController]
[Route("api/favorites")]
public class FavoritesController : ControllerBase
{
    private readonly FavoriteService _favorites;

    public FavoritesController(FavoriteService favorites)
    {
        _favorites = favorites;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var favorites = await _favorites.GetAllAsync();
        return Ok(favorites);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(int id)
    {
        var removed = await _favorites.RemoveByIdAsync(id);
        return removed ? Ok() : NotFound();
    }
}
