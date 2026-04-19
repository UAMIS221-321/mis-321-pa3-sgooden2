namespace MovieNight.Models;

public class Movie
{
    public int MovieId { get; set; }
    public string Title { get; set; } = "";
    public int Year { get; set; }
    public string Genre { get; set; } = "";
    public string Director { get; set; } = "";
    public string CastMembers { get; set; } = "";
    public string Plot { get; set; } = "";
    public decimal Rating { get; set; }
}
