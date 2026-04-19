using Dapper;
using MovieNight;
using MovieNight.Services;
using MySqlConnector;

// Allow Dapper to map snake_case columns (movie_id) to PascalCase properties (MovieId)
DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddHttpClient();
builder.Services.AddControllers();

builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<FavoriteService>();
builder.Services.AddScoped<AnthropicService>();

var app = builder.Build();

app.UseCors();

var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(wwwroot))
{
    app.UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = ["index.html"] });
    app.UseStaticFiles();
}

app.MapGet("/", () => Results.Redirect("/index.html"));
app.MapControllers();

await SetupDatabaseAsync(app.Services);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");


// ─── Database Setup ───────────────────────────────────────────────────────────

static async Task SetupDatabaseAsync(IServiceProvider services)
{
    try
    {
        var config = services.GetRequiredService<IConfiguration>();
        var connStr = Database.GetConnectionString(config);

        await using var conn = new MySqlConnection(connStr);
        await conn.OpenAsync();

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Movies (
                movie_id     INT AUTO_INCREMENT PRIMARY KEY,
                title        VARCHAR(255) NOT NULL,
                year         INT,
                genre        VARCHAR(255),
                director     VARCHAR(255),
                cast_members TEXT,
                plot         TEXT,
                rating       DECIMAL(3,1)
            )");

        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS Favorites (
                favorite_id INT AUTO_INCREMENT PRIMARY KEY,
                movie_id    INT NOT NULL,
                title       VARCHAR(255) NOT NULL,
                added_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (movie_id) REFERENCES Movies(movie_id)
            )");

        // Seed movies only once
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Movies");
        if (count == 0)
            await SeedMoviesAsync(conn);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Database setup failed — check connection string and MySQL password in appsettings.json");
    }
}

static async Task SeedMoviesAsync(MySqlConnection conn)
{
    var movies = new[]
    {
        new { title="Inception",                    year=2010, genre="Sci-Fi, Thriller",              director="Christopher Nolan",     cast="Leonardo DiCaprio, Joseph Gordon-Levitt, Elliot Page, Tom Hardy",   rating=8.8m, plot="A thief who enters the dreams of others to steal secrets is given the task of planting an idea into a CEO's mind." },
        new { title="The Dark Knight",              year=2008, genre="Action, Crime, Drama",           director="Christopher Nolan",     cast="Christian Bale, Heath Ledger, Aaron Eckhart, Maggie Gyllenhaal",    rating=9.0m, plot="Batman faces the Joker, a criminal mastermind who plunges Gotham City into anarchy and chaos." },
        new { title="Interstellar",                 year=2014, genre="Sci-Fi, Drama, Adventure",       director="Christopher Nolan",     cast="Matthew McConaughey, Anne Hathaway, Jessica Chastain, Michael Caine",rating=8.6m, plot="A team of explorers travel through a wormhole in space to ensure humanity's survival." },
        new { title="Pulp Fiction",                 year=1994, genre="Crime, Drama",                   director="Quentin Tarantino",     cast="John Travolta, Samuel L. Jackson, Uma Thurman, Bruce Willis",        rating=8.9m, plot="The lives of two mob hitmen, a boxer, and a gangster's wife intertwine in four tales of violence and redemption." },
        new { title="The Shawshank Redemption",     year=1994, genre="Drama",                          director="Frank Darabont",        cast="Tim Robbins, Morgan Freeman, Bob Gunton, William Sadler",            rating=9.3m, plot="Two imprisoned men bond over years, finding solace and redemption through acts of common decency." },
        new { title="The Matrix",                   year=1999, genre="Sci-Fi, Action",                 director="Lana Wachowski",        cast="Keanu Reeves, Laurence Fishburne, Carrie-Anne Moss, Hugo Weaving",   rating=8.7m, plot="A computer hacker learns that reality as he knows it is a simulation and joins a rebellion against the machines." },
        new { title="Forrest Gump",                 year=1994, genre="Drama, Romance",                 director="Robert Zemeckis",       cast="Tom Hanks, Robin Wright, Gary Sinise, Sally Field",                  rating=8.8m, plot="The story of a slow-witted but kind-hearted man from Alabama who witnesses major historical events." },
        new { title="Goodfellas",                   year=1990, genre="Crime, Drama, Biography",        director="Martin Scorsese",       cast="Ray Liotta, Robert De Niro, Joe Pesci, Lorraine Bracco",             rating=8.7m, plot="The story of Henry Hill's rise and fall within the New York mob." },
        new { title="Fight Club",                   year=1999, genre="Drama, Thriller",                director="David Fincher",         cast="Brad Pitt, Edward Norton, Helena Bonham Carter, Meat Loaf",          rating=8.8m, plot="An insomniac office worker and a soap salesman form an underground fight club that evolves into something sinister." },
        new { title="The Godfather",                year=1972, genre="Crime, Drama",                   director="Francis Ford Coppola",  cast="Marlon Brando, Al Pacino, James Caan, Diane Keaton",                rating=9.2m, plot="The aging patriarch of an organized crime dynasty transfers control of his empire to his reluctant son." },
        new { title="Parasite",                     year=2019, genre="Thriller, Drama, Dark Comedy",   director="Bong Joon-ho",          cast="Song Kang-ho, Lee Sun-kyun, Cho Yeo-jeong, Choi Woo-shik",          rating=8.5m, plot="A poor family schemes to become employed by a wealthy family, with unexpected consequences." },
        new { title="Avengers: Endgame",            year=2019, genre="Action, Adventure, Sci-Fi",      director="Anthony & Joe Russo",   cast="Robert Downey Jr., Chris Evans, Mark Ruffalo, Scarlett Johansson",   rating=8.4m, plot="The Avengers assemble once more to reverse the destruction caused by Thanos and restore balance to the universe." },
        new { title="Titanic",                      year=1997, genre="Drama, Romance",                 director="James Cameron",         cast="Leonardo DiCaprio, Kate Winslet, Billy Zane, Kathy Bates",           rating=7.9m, plot="A seventeen-year-old aristocrat falls in love with a poor artist aboard the ill-fated R.M.S. Titanic." },
        new { title="The Lion King",                year=1994, genre="Animation, Adventure, Family",   director="Roger Allers",          cast="Matthew Broderick, James Earl Jones, Jeremy Irons, Moira Kelly",     rating=8.5m, plot="A young lion prince flees his kingdom after the murder of his father, only to learn the true meaning of responsibility and bravery." },
        new { title="Spirited Away",                year=2001, genre="Animation, Adventure, Fantasy",  director="Hayao Miyazaki",        cast="Daveigh Chase, Suzanne Pleshette, Miyu Irino, Rumi Hiiragi",         rating=8.6m, plot="A young girl who, while moving with her family, becomes trapped in a mysterious spirit world." },
        new { title="Get Out",                      year=2017, genre="Horror, Mystery, Thriller",      director="Jordan Peele",          cast="Daniel Kaluuya, Allison Williams, Bradley Whitford, Catherine Keener",rating=7.7m, plot="A Black man visits his White girlfriend's family estate, where he discovers a disturbing secret." },
        new { title="La La Land",                   year=2016, genre="Drama, Romance, Musical",        director="Damien Chazelle",       cast="Ryan Gosling, Emma Stone, John Legend, Rosemarie DeWitt",            rating=8.0m, plot="A jazz pianist and an aspiring actress fall in love while pursuing their dreams in Los Angeles." },
        new { title="Whiplash",                     year=2014, genre="Drama, Music",                   director="Damien Chazelle",       cast="Miles Teller, J.K. Simmons, Melissa Benoist, Paul Reiser",           rating=8.5m, plot="A young drummer enrolls at a music conservatory and is pushed to his limits by a ruthless instructor." },
        new { title="Mad Max: Fury Road",           year=2015, genre="Action, Adventure, Sci-Fi",      director="George Miller",         cast="Tom Hardy, Charlize Theron, Nicholas Hoult, Hugh Keays-Byrne",       rating=8.1m, plot="In a post-apocalyptic wasteland, Max and Furiosa try to flee a cult leader and his army in a high-octane chase." },
        new { title="Everything Everywhere All at Once", year=2022, genre="Action, Sci-Fi, Comedy",   director="Daniel Kwan, Daniel Scheinert", cast="Michelle Yeoh, Ke Huy Quan, Jamie Lee Curtis, Stephanie Hsu", rating=7.8m, plot="A middle-aged Chinese-American laundromat owner is swept up in a multiverse adventure to save the world." },
        new { title="Dune",                         year=2021, genre="Sci-Fi, Adventure, Drama",       director="Denis Villeneuve",      cast="Timothée Chalamet, Zendaya, Rebecca Ferguson, Oscar Isaac",          rating=8.0m, plot="A noble family becomes embroiled in a war for control of the galaxy's most valuable asset on a desert planet." },
        new { title="The Grand Budapest Hotel",     year=2014, genre="Adventure, Comedy, Crime",       director="Wes Anderson",          cast="Ralph Fiennes, Tony Revolori, Saoirse Ronan, F. Murray Abraham",     rating=8.1m, plot="A concierge at a famous hotel between the wars befriends a lobby boy and becomes involved in the theft of a painting." },
        new { title="Black Panther",                year=2018, genre="Action, Adventure, Sci-Fi",      director="Ryan Coogler",          cast="Chadwick Boseman, Michael B. Jordan, Lupita Nyong'o, Danai Gurira",  rating=7.3m, plot="T'Challa returns home to Wakanda to claim the throne but faces a powerful enemy who challenges him for the crown." },
        new { title="The Prestige",                 year=2006, genre="Drama, Mystery, Sci-Fi",         director="Christopher Nolan",     cast="Christian Bale, Hugh Jackman, Scarlett Johansson, Michael Caine",    rating=8.5m, plot="Two stage magicians engage in competitive one-upmanship with tragic results." },
        new { title="Toy Story",                    year=1995, genre="Animation, Adventure, Comedy",   director="John Lasseter",         cast="Tom Hanks, Tim Allen, Don Rickles, Jim Varney",                      rating=8.3m, plot="A cowboy doll is threatened when a flashy space ranger toy becomes his owner's favorite." },
    };

    foreach (var m in movies)
    {
        await conn.ExecuteAsync(
            @"INSERT INTO Movies (title, year, genre, director, cast_members, plot, rating)
              VALUES (@title, @year, @genre, @director, @cast, @plot, @rating)",
            new { m.title, m.year, m.genre, m.director, cast = m.cast, m.plot, m.rating });
    }
}
