using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Wardrobe.Tests
{
    // 2026-09-30 polish pass: BuildInfo.g.cs is written by a target that runs after MSBuild has
    // already globbed the source files, so a fresh clone (the file is gitignored) failed its first
    // build with "The name 'BuildInfo' does not exist" and only built on the second try.
    public class BuildInfoTests
    {
        private static string Project()
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "Wardrobe", "Wardrobe.csproj")))
            {
                dir = Path.GetDirectoryName(dir);
            }
            Assert.True(dir != null, "no Wardrobe/Wardrobe.csproj above " + AppContext.BaseDirectory);
            return Path.Combine(dir!, "Wardrobe", "Wardrobe.csproj");
        }

        [Fact]
        public void TheTargetThatWritesBuildInfoAlsoCompilesIt()
        {
            XElement target = XDocument.Load(Project()).Descendants("Target")
                .Single(t => (string?)t.Attribute("Name") == "GenerateBuildInfo");
            Assert.Contains(target.Descendants("Compile"),
                c => ((string?)c.Attribute("Include") ?? "").EndsWith("BuildInfo.g.cs", StringComparison.Ordinal));
        }
    }
}
