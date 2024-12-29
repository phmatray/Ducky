// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Demo.BlazorWasm.AppStore;

[SuppressMessage("Roslynator", "RCS0056:A line is too long")]
public static class MoviesExamples
{
    public static readonly Movie Movie01 = new()
    {
        Id = 1,
        Title = "Forrest Gump",
        Year = 1994,
        Duration = "2h 22m",
        Rating = "PG-13",
        Imdb = 8.8,
        Metascore = 82,
        Description = "The history of the United States from the 1950s to the '70s unfolds from the perspective of an Alabama man with an IQ of 75, who yearns to be reunited with his childhood sweetheart.",
        Director = "Robert Zemeckis",
        Actors = new ValueCollection<string> { "Tom Hanks", "Robin Wright", "Gary Sinise" }
    };

    public static readonly Movie Movie02 = new()
    {
        Id = 2,
        Title = "The Shawshank Redemption",
        Year = 1994,
        Duration = "2h 22m",
        Rating = "R",
        Imdb = 9.3,
        Metascore = 82,
        Description = "Over the course of several years, two convicts form a friendship, seeking consolation and, eventually, redemption through basic compassion.",
        Director = "Frank Darabont",
        Actors = new ValueCollection<string> { "Tim Robbins", "Morgan Freeman", "Bob Gunton" }
    };

    public static readonly Movie Movie03 = new()
    {
        Id = 3,
        Title = "The Perks of Being a Wallflower",
        Year = 2012,
        Duration = "1h 43m",
        Rating = "PG-13",
        Imdb = 7.9,
        Metascore = 67,
        Description = "Charlie, a 15-year-old introvert, enters high school and is nervous about his new life. When he befriends his seniors, he learns to cope with his friend's suicide and his tumultuous past.",
        Director = "Stephen Chbosky",
        Actors = new ValueCollection<string> { "Logan Lerman", "Emma Watson", "Ezra Miller" }
    };

    public static readonly Movie Movie04 = new()
    {
        Id = 4,
        Title = "The Dark Knight",
        Year = 2008,
        Duration = "2h 32m",
        Rating = "PG-13",
        Imdb = 9.0,
        Metascore = 84,
        Description = "When the menace known as the Joker wreaks havoc and chaos on the people of Gotham, Batman must accept one of the greatest psychological and physical tests of his ability to fight injustice.",
        Director = "Christopher Nolan",
        Actors = new ValueCollection<string> { "Christian Bale", "Heath Ledger", "Aaron Eckhart" }
    };

    public static readonly Movie Movie05 = new()
    {
        Id = 5,
        Title = "Changeling",
        Year = 2008,
        Duration = "2h 21m",
        Rating = "R",
        Imdb = 7.7,
        Metascore = 63,
        Description = "After Christine's son goes missing, she reaches out to the LAPD to find him, but when they try to pass off an impostor as her son to quiet public protests, she refuses to accept him or give up hope.",
        Director = "Clint Eastwood",
        Actors = new ValueCollection<string> { "Angelina Jolie", "Colm Feore", "Amy Ryan" }
    };

    public static readonly Movie Movie06 = new()
    {
        Id = 6,
        Title = "This Boy's Life",
        Year = 1993,
        Duration = "1h 55m",
        Rating = "R",
        Imdb = 7.3,
        Metascore = 60,
        Description = "The story about the relationship between a rebellious 1950s teenager and his abusive stepfather, based on the memoirs of writer and literature Professor Tobias Wolff.",
        Director = "Michael Caton-Jones",
        Actors = new ValueCollection<string> { "Robert De Niro", "Leonardo DiCaprio", "Ellen Barkin" }
    };

    public static readonly Movie Movie07 = new()
    {
        Id = 7,
        Title = "It's a Wonderful Life",
        Year = 1946,
        Duration = "2h 10m",
        Rating = "PG",
        Imdb = 8.6,
        Metascore = 89,
        Description = "An angel is sent from Heaven to help a desperately frustrated businessman by showing him what life would have been like if he had never existed.",
        Director = "Frank Capra",
        Actors = new ValueCollection<string> { "James Stewart", "Donna Reed", "Lionel Barrymore" }
    };

    public static readonly Movie Movie08 = new()
    {
        Id = 8,
        Title = "The Silence of the Lambs",
        Year = 1991,
        Duration = "1h 58m",
        Rating = "R",
        Imdb = 8.6,
        Metascore = 86,
        Description = "A young F.B.I. cadet must receive the help of an incarcerated and manipulative cannibal killer to help catch another serial killer, a madman who skins his victims.",
        Director = "Jonathan Demme",
        Actors = new ValueCollection<string> { "Jodie Foster", "Anthony Hopkins", "Scott Glenn" }
    };

    public static readonly Movie Movie09 = new()
    {
        Id = 9,
        Title = "8 Mile",
        Year = 2002,
        Duration = "1h 50m",
        Rating = "R",
        Imdb = 7.2,
        Metascore = 77,
        Description = "Follows a young rapper in the Detroit area, struggling with every aspect of his life; he wants to make it big but his friends and foes make this odyssey of rap harder than it may seem.",
        Director = "Curtis Hanson",
        Actors = new ValueCollection<string> { "Eminem", "Brittany Murphy", "Kim Basinger" }
    };

    public static readonly Movie Movie10 = new()
    {
        Id = 10,
        Title = "The Breakfast Club",
        Year = 1985,
        Duration = "1h 37m",
        Rating = "R",
        Imdb = 7.8,
        Metascore = 66,
        Description = "Five high school students meet in Saturday detention and discover how they have a great deal more in common than they thought.",
        Director = "John Hughes",
        Actors = new ValueCollection<string> { "Emilio Estevez", "Judd Nelson", "Molly Ringwald" }
    };

    public static readonly Movie Movie11 = new()
    {
        Id = 11,
        Title = "Django Unchained",
        Year = 2012,
        Duration = "2h 45m",
        Rating = "R",
        Imdb = 8.5,
        Metascore = 81,
        Description = "With the help of a German bounty-hunter, a freed slave sets out to rescue his wife from a brutal plantation owner in Mississippi.",
        Director = "Quentin Tarantino",
        Actors = new ValueCollection<string> { "Jamie Foxx", "Christoph Waltz", "Leonardo DiCaprio" }
    };

    public static readonly Movie Movie12 = new()
    {
        Id = 12,
        Title = "Silver Linings Playbook",
        Year = 2012,
        Duration = "2h 2m",
        Rating = "R",
        Imdb = 7.7,
        Metascore = 81,
        Description = "After a stint in a mental institution, former teacher Pat Solitano moves back in with his parents and tries to reconcile with his ex-wife. Things get more challenging when Pat meets Tiffany, a mysterious girl with problems of her own.",
        Director = "David O. Russell",
        Actors = new ValueCollection<string> { "Bradley Cooper", "Jennifer Lawrence", "Robert De Niro" }
    };

    public static readonly Movie Movie13 = new()
    {
        Id = 13,
        Title = "The Shining",
        Year = 1980,
        Duration = "2h 26m",
        Rating = "R",
        Imdb = 8.4,
        Metascore = 68,
        Description = "A family heads to an isolated hotel for the winter where a sinister presence influences the father into violence, while his psychic son sees horrific forebodings from both past and future.",
        Director = "Stanley Kubrick",
        Actors = new ValueCollection<string> { "Jack Nicholson", "Shelley Duvall", "Danny Lloyd" }
    };

    public static readonly Movie Movie14 = new()
    {
        Id = 14,
        Title = "Se7en",
        Year = 1995,
        Duration = "2h 7m",
        Rating = "R",
        Imdb = 8.6,
        Metascore = 65,
        Description = "Two detectives, a rookie and a veteran, hunt a serial killer who uses the seven deadly sins as his motives.",
        Director = "David Fincher",
        Actors = new ValueCollection<string> { "Morgan Freeman", "Brad Pitt", "Kevin Spacey" }
    };

    public static readonly Movie Movie15 = new()
    {
        Id = 15,
        Title = "American Beauty",
        Year = 1999,
        Duration = "2h 2m",
        Rating = "R",
        Imdb = 8.3,
        Metascore = 84,
        Description = "A sexually frustrated suburban father has a mid-life crisis after becoming infatuated with his daughter's best friend.",
        Director = "Sam Mendes",
        Actors = new ValueCollection<string> { "Kevin Spacey", "Annette Bening", "Thora Birch" }
    };

    public static readonly Movie Movie16 = new()
    {
        Id = 16,
        Title = "Pulp Fiction",
        Year = 1994,
        Duration = "2h 34m",
        Rating = "R",
        Imdb = 8.9,
        Metascore = 95,
        Description = "The lives of two mob hitmen, a boxer, a gangster and his wife, and a pair of diner bandits intertwine in four tales of violence and redemption.",
        Director = "Quentin Tarantino",
        Actors = new ValueCollection<string> { "John Travolta", "Uma Thurman", "Samuel L. Jackson" }
    };

    public static readonly Movie Movie17 = new()
    {
        Id = 17,
        Title = "Zero Dark Thirty",
        Year = 2012,
        Duration = "2h 37m",
        Rating = "R",
        Imdb = 7.4,
        Metascore = 95,
        Description = "A chronicle of the decade-long hunt for al-Qaeda terrorist leader Osama bin Laden after the September 2001 attacks, and his death at the hands of the Navy S.E.A.L.s Team 6 in May 2011.",
        Director = "Kathryn Bigelow",
        Actors = new ValueCollection<string> { "Jessica Chastain", "Joel Edgerton", "Chris Pratt" }
    };

    public static readonly Movie Movie18 = new()
    {
        Id = 18,
        Title = "Argo",
        Year = 2012,
        Duration = "2h",
        Rating = "R",
        Imdb = 7.7,
        Metascore = 86,
        Description = "Acting under the cover of a Hollywood producer scouting a location for a science fiction film, a CIA agent launches a dangerous operation to rescue six Americans in Tehran during the U.S. hostage crisis in Iran in 1979.",
        Director = "Ben Affleck",
        Actors = new ValueCollection<string> { "Ben Affleck", "Bryan Cranston", "John Goodman" }
    };

    public static readonly Movie Movie19 = new()
    {
        Id = 19,
        Title = "The Hurt Locker",
        Year = 2008,
        Duration = "2h 11m",
        Rating = "R",
        Imdb = 7.5,
        Metascore = 95,
        Description = "During the Iraq War, a Sergeant recently assigned to an army bomb squad is put at odds with his squad mates due to his maverick way of handling his work.",
        Director = "Kathryn Bigelow",
        Actors = new ValueCollection<string> { "Jeremy Renner", "Anthony Mackie", "Brian Geraghty" }
    };

    public static readonly Movie Movie20 = new()
    {
        Id = 20,
        Title = "The Godfather",
        Year = 1972,
        Duration = "2h 55m",
        Rating = "R",
        Imdb = 9.2,
        Metascore = 100,
        Description = "Don Vito Corleone, head of a mafia family, decides to hand over his empire to his youngest son, Michael. However, his decision unintentionally puts the lives of his loved ones in grave danger.",
        Director = "Francis Ford Coppola",
        Actors = new ValueCollection<string> { "Marlon Brando", "Al Pacino", "James Caan" }
    };

    public static readonly Movie Movie21 = new()
    {
        Id = 21,
        Title = "The Town",
        Year = 2010,
        Duration = "2h 5m",
        Rating = "R",
        Imdb = 7.5,
        Metascore = 74,
        Description = "A proficient group of thieves rob a bank and hold an assistant manager hostage. Things begin to get complicated when one of the crew members falls in love with her.",
        Director = "Ben Affleck",
        Actors = new ValueCollection<string> { "Ben Affleck", "Rebecca Hall", "Jon Hamm" }
    };

    public static readonly Movie Movie22 = new()
    {
        Id = 22,
        Title = "The Departed",
        Year = 2006,
        Duration = "2h 31m",
        Rating = "R",
        Imdb = 8.5,
        Metascore = 85,
        Description = "An undercover cop and a mole in the police attempt to identify each other while infiltrating an Irish gang in South Boston.",
        Director = "Martin Scorsese",
        Actors = new ValueCollection<string> { "Leonardo DiCaprio", "Matt Damon", "Jack Nicholson" }
    };

    public static readonly Movie Movie23 = new()
    {
        Id = 23,
        Title = "Scream",
        Year = 1996,
        Duration = "1h 51m",
        Rating = "R",
        Imdb = 7.4,
        Metascore = 66,
        Description = "A year after the murder of her mother, a teenage girl is terrorized by a masked killer who targets her and her friends by using scary movies as part of a deadly game.",
        Director = "Wes Craven",
        Actors = new ValueCollection<string> { "Neve Campbell", "Courteney Cox", "David Arquette" }
    };

    public static readonly Movie Movie24 = new()
    {
        Id = 24,
        Title = "Up in the Air",
        Year = 2009,
        Duration = "1h 49m",
        Rating = "R",
        Imdb = 7.4,
        Metascore = 83,
        Description = "Ryan's job is to travel around the country firing off people. When his boss hires Natalie, who proposes firing people via video conference, he tries to convince her that her method is a mistake.",
        Director = "Jason Reitman",
        Actors = new ValueCollection<string> { "George Clooney", "Vera Farmiga", "Anna Kendrick" }
    };

    public static readonly Movie Movie25 = new()
    {
        Id = 25,
        Title = "What's Eating Gilbert Grape",
        Year = 1993,
        Duration = "1h 58m",
        Rating = "PG-13",
        Imdb = 7.7,
        Metascore = 73,
        Description = "A young man in a small Midwestern town struggles to care for his mentally-disabled younger brother and morbidly obese mother while attempting to pursue his own happiness.",
        Director = "Lasse Hallström",
        Actors = new ValueCollection<string> { "Johnny Depp", "Leonardo DiCaprio", "Juliette Lewis" }
    };

    public static readonly Movie Movie26 = new()
    {
        Id = 26,
        Title = "Lost in Translation",
        Year = 2003,
        Duration = "1h 42m",
        Rating = "R",
        Imdb = 7.7,
        Metascore = 91,
        Description = "A faded movie star and a neglected young woman form an unlikely bond after crossing paths in Tokyo.",
        Director = "Sofia Coppola",
        Actors = new ValueCollection<string> { "Bill Murray", "Scarlett Johansson", "Giovanni Ribisi" }
    };

    public static readonly Movie Movie27 = new()
    {
        Id = 27,
        Title = "The Conjuring",
        Year = 2013,
        Duration = "1h 52m",
        Rating = "R",
        Imdb = 7.5,
        Metascore = 68,
        Description = "Paranormal investigators Ed and Lorraine Warren work to help a family terrorized by a dark presence in their farmhouse.",
        Director = "James Wan",
        Actors = new ValueCollection<string> { "Patrick Wilson", "Vera Farmiga", "Ron Livingston" }
    };

    public static readonly Movie Movie28 = new()
    {
        Id = 28,
        Title = "Juno",
        Year = 2007,
        Duration = "1h 36m",
        Rating = "PG-13",
        Imdb = 7.4,
        Metascore = 81,
        Description = "Faced with an unplanned pregnancy, an offbeat young woman makes a selfless decision regarding the unborn child.",
        Director = "Jason Reitman",
        Actors = new ValueCollection<string> { "Elliot Page", "Michael Cera", "Jennifer Garner" }
    };

    public static readonly Movie Movie29 = new()
    {
        Id = 29,
        Title = "Stand by Me",
        Year = 1986,
        Duration = "1h 29m",
        Rating = "R",
        Imdb = 8.1,
        Metascore = 75,
        Description = "A writer recounts a childhood journey with his friends to find the body of a missing boy.",
        Director = "Rob Reiner",
        Actors = new ValueCollection<string> { "Wil Wheaton", "River Phoenix", "Corey Feldman" }
    };

    public static readonly Movie Movie30 = new()
    {
        Id = 30,
        Title = "The Green Mile",
        Year = 1999,
        Duration = "3h 9m",
        Rating = "R",
        Imdb = 8.6,
        Metascore = 61,
        Description = "A tale set on death row, where gentle giant John Coffey possesses the mysterious power to heal people's ailments. When the lead guard, Paul Edgecombe, recognizes John's gift, he tries to help stave off the condemned man's execution.",
        Director = "Frank Darabont",
        Actors = new ValueCollection<string> { "Tom Hanks", "Michael Clarke Duncan", "David Morse" }
    };

    public static readonly Movie Movie31 = new()
    {
        Id = 31,
        Title = "Super 8",
        Year = 2011,
        Duration = "1h 52m",
        Rating = "PG-13",
        Imdb = 7.0,
        Metascore = 72,
        Description = "During the summer of 1979, a group of friends witness a train crash and investigate subsequent unexplained events in their small town.",
        Director = "J.J. Abrams",
        Actors = new ValueCollection<string> { "Elle Fanning", "AJ Michalka", "Kyle Chandler" }
    };

    public static readonly Movie Movie32 = new()
    {
        Id = 32,
        Title = "Jarhead",
        Year = 2005,
        Duration = "2h 5m",
        Rating = "R",
        Imdb = 7.0,
        Metascore = 58,
        Description = "A psychological study of Marine's state of mind during the Gulf War. Told through the eyes of a U.S. Marine sniper who struggles to cope with boredom, a sense of isolation, and other issues back home.",
        Director = "Sam Mendes",
        Actors = new ValueCollection<string> { "Jake Gyllenhaal", "Jamie Foxx", "Lucas Black" }
    };

    public static readonly Movie Movie33 = new()
    {
        Id = 33,
        Title = "Misery",
        Year = 1990,
        Duration = "1h 47m",
        Rating = "R",
        Imdb = 7.8,
        Metascore = 75,
        Description = "After a famous author is rescued from a car crash by a fan of his novels, he comes to realize that the care he is receiving is only the beginning of a nightmare of captivity and abuse.",
        Director = "Rob Reiner",
        Actors = new ValueCollection<string> { "James Caan", "Kathy Bates", "Richard Farnsworth" }
    };

    public static readonly Movie Movie34 = new()
    {
        Id = 34,
        Title = "Fight Club",
        Year = 1999,
        Duration = "2h 19m",
        Rating = "R",
        Imdb = 8.8,
        Metascore = 67,
        Description = "An insomniac office worker and a devil-may-care soap maker form an underground fight club that evolves into much more.",
        Director = "David Fincher",
        Actors = new ValueCollection<string> { "Brad Pitt", "Edward Norton", "Meat Loaf" }
    };

    public static readonly Movie Movie35 = new()
    {
        Id = 35,
        Title = "Shutter Island",
        Year = 2010,
        Duration = "2h 18m",
        Rating = "R",
        Imdb = 8.2,
        Metascore = 63,
        Description = "Teddy Daniels and Chuck Aule, two US marshals, are sent to an asylum on a remote island in order to investigate the disappearance of a patient, where Teddy uncovers a shocking truth about the place.",
        Director = "Martin Scorsese",
        Actors = new ValueCollection<string> { "Leonardo DiCaprio", "Emily Mortimer", "Mark Ruffalo" }
    };

    public static readonly Movie Movie36 = new()
    {
        Id = 36,
        Title = "Lawless",
        Year = 2012,
        Duration = "1h 56m",
        Rating = "R",
        Imdb = 7.2,
        Metascore = 58,
        Description = "Set in Depression-era Franklin County, Virginia, a trio of bootlegging brothers are threatened by a new special deputy and other authorities angling for a cut of their profits.",
        Director = "John Hillcoat",
        Actors = new ValueCollection<string> { "Tom Hardy", "Shia LaBeouf", "Guy Pearce" }
    };

    public static readonly Movie Movie37 = new()
    {
        Id = 37,
        Title = "Winter's Bone",
        Year = 2010,
        Duration = "1h 40m",
        Rating = "R",
        Imdb = 7.1,
        Metascore = 90,
        Description = "An unflinching Ozark Mountain girl hacks through dangerous social terrain as she hunts down her drug-dealing father while trying to keep her family intact.",
        Director = "Debra Granik",
        Actors = new ValueCollection<string> { "Jennifer Lawrence", "John Hawkes", "Garret Dillahunt" }
    };

    public static readonly Movie Movie38 = new()
    {
        Id = 38,
        Title = "Taxi Driver",
        Year = 1976,
        Duration = "1h 54m",
        Rating = "R",
        Imdb = 8.2,
        Metascore = 94,
        Description = "A mentally unstable veteran works as a nighttime taxi driver in New York City, where the perceived decadence and sleaze fuels his urge for violent action.",
        Director = "Martin Scorsese",
        Actors = new ValueCollection<string> { "Robert De Niro", "Jodie Foster", "Cybill Shepherd" }
    };

    public static readonly Movie Movie39 = new()
    {
        Id = 39,
        Title = "Saving Private Ryan",
        Year = 1998,
        Duration = "2h 49m",
        Rating = "R",
        Imdb = 8.6,
        Metascore = 91,
        Description = "Following the Normandy Landings, a group of U.S. soldiers go behind enemy lines to retrieve a paratrooper whose brothers have been killed in action.",
        Director = "Steven Spielberg",
        Actors = new ValueCollection<string> { "Tom Hanks", "Matt Damon", "Tom Sizemore" }
    };

    public static readonly Movie Movie40 = new()
    {
        Id = 40,
        Title = "Black Swan",
        Year = 2010,
        Duration = "1h 48m",
        Rating = "R",
        Imdb = 8.0,
        Metascore = 79,
        Description = "Nina is a talented but unstable ballerina on the verge of stardom. Pushed to the breaking point by her artistic director and a seductive rival, Nina's grip on reality slips, plunging her into a waking nightmare.",
        Director = "Darren Aronofsky",
        Actors = new ValueCollection<string> { "Natalie Portman", "Mila Kunis", "Vincent Cassel" }
    };

    public static readonly Movie Movie41 = new()
    {
        Id = 41,
        Title = "Inception",
        Year = 2010,
        Duration = "2h 28m",
        Rating = "PG-13",
        Imdb = 8.8,
        Metascore = 74,
        Description = "A thief who steals corporate secrets through the use of dream-sharing technology is given the inverse task of planting an idea into the mind of a C.E.O., but his tragic past may doom the project and his team to disaster.",
        Director = "Christopher Nolan",
        Actors = new ValueCollection<string> { "Leonardo DiCaprio", "Joseph Gordon-Levitt", "Elliot Page" }
    };

    public static readonly Movie Movie42 = new()
    {
        Id = 42,
        Title = "Boogie Nights",
        Year = 1997,
        Duration = "2h 35m",
        Rating = "R",
        Imdb = 7.9,
        Metascore = 86,
        Description = "Back when sex was safe, pleasure was a business and business was booming, an idealistic porn producer aspires to elevate his craft to an art when he discovers a hot young talent.",
        Director = "Paul Thomas Anderson",
        Actors = new ValueCollection<string> { "Mark Wahlberg", "Julianne Moore", "Burt Reynolds" }
    };

    public static readonly Movie Movie43 = new()
    {
        Id = 43,
        Title = "50/50",
        Year = 2011,
        Duration = "1h 40m",
        Rating = "R",
        Imdb = 7.6,
        Metascore = 72,
        Description = "Inspired by a true story, a comedy centered on a 27-year-old guy who learns of his cancer diagnosis and his subsequent struggle to beat the disease.",
        Director = "Jonathan Levine",
        Actors = new ValueCollection<string> { "Joseph Gordon-Levitt", "Seth Rogen", "Anna Kendrick" }
    };

    public static readonly Movie Movie44 = new()
    {
        Id = 44,
        Title = "Brothers",
        Year = 2009,
        Duration = "1h 45m",
        Rating = "R",
        Imdb = 7.1,
        Metascore = 58,
        Description = "While on tour in Afghanistan, Sam's helicopter is shot down and he is presumed dead. Back home, it is his screw-up brother who looks after the family. Sam does return, but with a lot of excess baggage.",
        Director = "Jim Sheridan",
        Actors = new ValueCollection<string> { "Jake Gyllenhaal", "Natalie Portman", "Tobey Maguire" }
    };

    public static readonly Movie Movie45 = new()
    {
        Id = 45,
        Title = "Blood Diamond",
        Year = 2006,
        Duration = "2h 23m",
        Rating = "R",
        Imdb = 8.0,
        Metascore = 64,
        Description = "A fisherman, a smuggler, and a syndicate of businessmen match wits over the possession of a priceless diamond.",
        Director = "Edward Zwick",
        Actors = new ValueCollection<string> { "Leonardo DiCaprio", "Djimon Hounsou", "Jennifer Connelly" }
    };

    public static readonly Movie Movie46 = new()
    {
        Id = 46,
        Title = "A Few Good Men",
        Year = 1992,
        Duration = "2h 18m",
        Rating = "R",
        Imdb = 7.7,
        Metascore = 62,
        Description = "Military lawyer Lieutenant Daniel Kaffee defends Marines accused of murder. They contend they were acting under orders.",
        Director = "Rob Reiner",
        Actors = new ValueCollection<string> { "Tom Cruise", "Jack Nicholson", "Demi Moore" }
    };

    public static readonly Movie Movie47 = new()
    {
        Id = 47,
        Title = "Gladiator",
        Year = 2000,
        Duration = "2h 35m",
        Rating = "R",
        Imdb = 8.5,
        Metascore = 67,
        Description = "A former Roman General sets out to exact vengeance against the corrupt emperor who murdered his family and sent him into slavery.",
        Director = "Ridley Scott",
        Actors = new ValueCollection<string> { "Russell Crowe", "Joaquin Phoenix", "Connie Nielsen" }
    };

    public static readonly Movie Movie48 = new()
    {
        Id = 48,
        Title = "Law Abiding Citizen",
        Year = 2009,
        Duration = "1h 49m",
        Rating = "R",
        Imdb = 7.4,
        Metascore = 34,
        Description = "A frustrated man decides to take justice into his own hands after a plea bargain sets one of his family's killers free.",
        Director = "F. Gary Gray",
        Actors = new ValueCollection<string> { "Gerard Butler", "Jamie Foxx", "Leslie Bibb" }
    };

    public static readonly Movie Movie49 = new()
    {
        Id = 49,
        Title = "Lakeview Terrace",
        Year = 2008,
        Duration = "1h 50m",
        Rating = "PG-13",
        Imdb = 6.2,
        Metascore = 47,
        Description = "A troubled and racist African-American L.A.P.D. Officer will stop at nothing to force out a friendly interracial couple who just moved in next door to him.",
        Director = "Neil LaBute",
        Actors = new ValueCollection<string> { "Samuel L. Jackson", "Patrick Wilson", "Kerry Washington" }
    };

    public static readonly Movie Movie50 = new()
    {
        Id = 50,
        Title = "Glory Road",
        Year = 2006,
        Duration = "1h 58m",
        Rating = "PG",
        Imdb = 7.2,
        Metascore = 58,
        Description = "In 1966, Texas Western coach Don Haskins led the first all-black starting line-up for a college basketball team to the NCAA national championship.",
        Director = "James Gartner",
        Actors = new ValueCollection<string> { "Josh Lucas", "Derek Luke", "Austin Nichols" }
    };

    public static readonly ValueCollection<Movie> Movies =
    [
        Movie01, Movie02, Movie03, Movie04, Movie05,
        Movie06, Movie07, Movie08, Movie09, Movie10,
        Movie11, Movie12, Movie13, Movie14, Movie15,
        Movie16, Movie17, Movie18, Movie19, Movie20,
        Movie21, Movie22, Movie23, Movie24, Movie25,
        Movie26, Movie27, Movie28, Movie29, Movie30,
        Movie31, Movie32, Movie33, Movie34, Movie35,
        Movie36, Movie37, Movie38, Movie39, Movie40,
        Movie41, Movie42, Movie43, Movie44, Movie45,
        Movie46, Movie47, Movie48, Movie49, Movie50
    ];
}
