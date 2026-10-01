using System;
using System.Collections.Generic;
using System.Linq;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // The wheel is the starred categories, A to Z with a leading "The" ignored, each holding its
    // looks in the category's own order. Other mods can narrow it; a category they empty goes.
    public class WheelLineupTests
    {
        private static LookGroup Group(string title, params string[] looks)
        {
            var group = new LookGroup { Title = title };
            for (int i = 0; i < looks.Length; i++)
            {
                group.Slots.Add(title.Length * 100 + i);
                group.Names.Add(looks[i]);
            }
            return group;
        }

        private static readonly List<LookGroup> Groups = new List<LookGroup>
        {
            Group("Movies", "Vader", "Indy"),
            Group("The Simpsons", "Homer", "Marge", "Bart"),
            Group("Anime", "Goku"),
            Group("Doctor Who", "Tenth", "Dalek"),
            Group("My looks", "Mine")
        };

        private static readonly Func<string, bool> All = _ => true;

        [Fact]
        public void StarredCategoriesGoAToZIgnoringThe()
        {
            // favourites.txt lists them in the order they were starred; the wheel does not care.
            var starred = new List<string> { "The Simpsons", "Movies", "Doctor Who", "Anime" };

            List<WheelFolder> wheel = WheelLineup.Build(Groups, starred, All);

            Assert.Equal(new[] { "Anime", "Doctor Who", "Movies", "The Simpsons" }, wheel.Select(f => f.Title));
        }

        [Fact]
        public void OnlyStarredCategoriesAreOnTheWheel()
        {
            List<WheelFolder> wheel = WheelLineup.Build(Groups, new List<string> { "movies" }, All);

            Assert.Equal(new[] { "Movies" }, wheel.Select(f => f.Title));
        }

        [Fact]
        public void LooksKeepTheCategorysOwnOrder()
        {
            List<WheelFolder> wheel = WheelLineup.Build(Groups, new List<string> { "The Simpsons" }, All);

            Assert.Equal(new[] { "Homer", "Marge", "Bart" }, wheel[0].Names);
        }

        [Fact]
        public void AFilterHidesLooksAndACategoryItEmpties()
        {
            var starred = new List<string> { "Movies", "The Simpsons", "Anime" };
            var withSounds = new HashSet<string> { "Bart", "Homer", "Goku" };

            List<WheelFolder> wheel = WheelLineup.Build(Groups, starred, withSounds.Contains);

            Assert.Equal(new[] { "Anime", "The Simpsons" }, wheel.Select(f => f.Title));
            Assert.Equal(new[] { "Homer", "Bart" }, wheel[1].Names);
        }

        [Fact]
        public void SortKeyOnlyDropsALeadingThe()
        {
            Assert.Equal("Simpsons", WheelOrder.SortKey("The Simpsons"));
            Assert.Equal("Theatre", WheelOrder.SortKey("Theatre"));
            Assert.Equal("The", WheelOrder.SortKey("The"));
        }
    }

    // Shipped and pack categories come starred the first time they are seen; yours never do, and
    // a category you unstarred stays that way through every update.
    public class StarSeedingTests
    {
        [Fact]
        public void AShippedCategoryIsStarredTheFirstTimeItIsSeen()
        {
            var favourites = new FavouriteFolders();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Assert.True(favourites.Offer("Cartoons", seen));
            Assert.True(favourites.Has("Cartoons"));
            Assert.Contains("cartoons", seen);
        }

        [Fact]
        public void AnUnstarIsRememberedAndNeverUndone()
        {
            var favourites = new FavouriteFolders();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            favourites.Offer("Cartoons", seen);

            favourites.Toggle("Cartoons");

            Assert.False(favourites.Offer("Cartoons", seen));
            Assert.False(favourites.Has("Cartoons"));
        }

        [Fact]
        public void NothingIsStarredThatWasNotOffered()
        {
            // Your own folders are never offered, so they are never starred behind your back.
            var favourites = new FavouriteFolders();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string shipped in new[] { "Anime", "Games" })
            {
                favourites.Offer(shipped, seen);
            }

            Assert.False(favourites.Has("My looks"));
            Assert.False(favourites.Has("Friends"));
            Assert.Equal(2, favourites.Count);
        }
    }

    // Shipped looks keep the order their file lists them in, per category, so a category reads most
    // famous first instead of A to Z.
    public class LibraryOrderTests
    {
        private static KeyValuePair<string, string> Look(string name, string category) => new KeyValuePair<string, string>(name, category);

        [Fact]
        public void EachCategoryCountsFromZeroInFileOrder()
        {
            Dictionary<string, int> order = LibraryOrder.Positions(new[]
            {
                Look("Mario", "Games"),
                Look("Homer", "The Simpsons"),
                Look("Luigi", "Games"),
                Look("Bart", "The Simpsons"),
                Look("Zelda", "Games")
            });

            Assert.Equal(0, order["Mario"]);
            Assert.Equal(1, order["Luigi"]);
            Assert.Equal(2, order["Zelda"]);
            Assert.Equal(0, order["Homer"]);
            Assert.Equal(1, order["Bart"]);
        }

        [Fact]
        public void CategoriesMatchWhateverWayTheyAreSpelled()
        {
            Dictionary<string, int> order = LibraryOrder.Positions(new[] { Look("A", "games"), Look("B", "Games") });

            Assert.Equal(1, order["B"]);
        }
    }
}
