using System;

namespace Wardrobe.Services
{
    // A five-pointed star, point up, drawn into a square. The game's font has no star glyph worth
    // trusting, so the header star is a texture made from this.
    internal static class StarShape
    {
        private const int Samples = 4;
        private const float Hollow = 0.55f;

        // How much of pixel (x, y) the star covers, 0 to 1, with y counted up from the bottom the
        // way a texture is.
        public static float Coverage(int x, int y, int size, bool filled)
        {
            float centre = size / 2f;
            float outer = size * 0.48f;
            int hits = 0;
            for (int i = 0; i < Samples; i++)
            {
                for (int j = 0; j < Samples; j++)
                {
                    float px = x + (i + 0.5f) / Samples - centre;
                    float py = y + (j + 0.5f) / Samples - centre;
                    if (Inside(px, py, outer) && (filled || !Inside(px, py, outer * Hollow)))
                    {
                        hits++;
                    }
                }
            }
            return hits / (float)(Samples * Samples);
        }

        // Even-odd test against the ten corners, alternating the tips and the notches between them.
        private static bool Inside(float px, float py, float outer)
        {
            float inner = outer * 0.42f;
            bool inside = false;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                Corner(i, outer, inner, out float xi, out float yi);
                Corner(j, outer, inner, out float xj, out float yj);
                if ((yi > py) != (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static void Corner(int index, float outer, float inner, out float x, out float y)
        {
            double angle = Math.PI / 2 + index * Math.PI / 5;
            float radius = index % 2 == 0 ? outer : inner;
            x = (float)(Math.Cos(angle) * radius);
            y = (float)(Math.Sin(angle) * radius);
        }
    }

    internal static class FolderStar
    {
        // Turns a folder on or off the wheel and says which, the way the tab says it.
        public static string Flip(FavouriteFolders favourites, string folder)
        {
            return folder + (favourites.Toggle(folder) ? " is on the wheel" : " is off the wheel");
        }
    }
}
