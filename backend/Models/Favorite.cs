namespace MovieNight.Models;

public class Favorite
{
    public int FavoriteId { get; set; }
    public int MovieId { get; set; }
    public string Title { get; set; } = "";
    public string Genre { get; set; } = "";
    public int Year { get; set; }
    public DateTime AddedAt { get; set; }
}
