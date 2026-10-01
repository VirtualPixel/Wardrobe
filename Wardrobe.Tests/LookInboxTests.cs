using System.Collections.Generic;
using Wardrobe.Services;
using Xunit;

namespace Wardrobe.Tests
{
    // What a pasted code does to the library: every arrival gets a name of its own, and a look you
    // already have in that folder, piece for piece, is not added a second time.
    public class LookInboxTests
    {
        private static IniSection Section(string text)
        {
            Assert.True(LookSection.TryParse(text, out IniSection? section, out string problem), problem);
            return section!;
        }

        private static IniFile Library(params string[] lines) => IniFile.Parse(lines);

        private static List<string> Names(InboxPlan plan)
        {
            var names = new List<string>();
            foreach (IniSection section in plan.Adds)
            {
                names.Add(LookSection.Name(section));
            }
            return names;
        }

        [Fact]
        public void EveryLookLandsInTheFolderItWasPastedInto()
        {
            InboxPlan plan = LookInbox.Plan(Library(), new[] { Section("[Goofy]\ncategory = Theirs\nhat = Top Hat\n") }, "Cartoons");

            Assert.Equal("Cartoons", LookSection.Category(plan.Adds[0]));
            Assert.Equal("Top Hat", plan.Adds[0].Get("hat"));
        }

        [Fact]
        public void ANameYouAlreadyHaveGetsANumberInstead()
        {
            IniFile library = Library("[Goofy]", "category = Cartoons", "hat = Cone");

            InboxPlan plan = LookInbox.Plan(library, new[] { Section("[Goofy]\nhat = Top Hat\n") }, "Cartoons");

            Assert.Equal(new[] { "Goofy 2" }, Names(plan));
            Assert.Equal(0, plan.AlreadyThere);
        }

        [Fact]
        public void TheSameLookInTheSameFolderIsNotAddedTwice()
        {
            IniFile library = Library("[Goofy]", "category = Cartoons", "hat = Top Hat", "color.hat = Forest Green");

            InboxPlan plan = LookInbox.Plan(library, new[] { Section("[Goofy]\nhat = top hat\ncolor.hat = Forest Green\n") }, "cartoons");

            Assert.Empty(plan.Adds);
            Assert.Equal(1, plan.AlreadyThere);
        }

        [Fact]
        public void TheSameLookFiledSomewhereElseIsStillAdded()
        {
            IniFile library = Library("[Goofy]", "category = Disney", "hat = Top Hat");

            InboxPlan plan = LookInbox.Plan(library, new[] { Section("[Goofy]\nhat = Top Hat\n") }, "Cartoons");

            Assert.Equal(new[] { "Goofy 2" }, Names(plan));
        }

        [Fact]
        public void TwoArrivalsWithOneNameDoNotLandOnEachOther()
        {
            InboxPlan plan = LookInbox.Plan(Library("[Mario]", "hat = Cone"),
                new[] { Section("[Mario]\nhat = Cap\n"), Section("[Mario]\nhat = Crown\n"), Section("[Mario 2]\nhat = Fez\n") }, "Games");

            Assert.Equal(new[] { "Mario 2", "Mario 3", "Mario 2 2" }, Names(plan));
        }

        [Fact]
        public void ANameOnALookYouMadeInGameIsTakenToo()
        {
            InboxPlan plan = LookInbox.Plan(Library(), new[] { Section("[Luigi]\nhat = Cap\n") }, "Games", name => name == "Luigi");

            Assert.Equal(new[] { "Luigi 2" }, Names(plan));
        }

        [Fact]
        public void AnEmptyFolderNameFallsBackToTheDefaultOne()
        {
            InboxPlan plan = LookInbox.Plan(Library(), new[] { Section("[Luigi]\nhat = Cap\n") }, "  ");

            Assert.Equal(PresetNames.DefaultCategory, plan.Folder);
        }
    }
}
