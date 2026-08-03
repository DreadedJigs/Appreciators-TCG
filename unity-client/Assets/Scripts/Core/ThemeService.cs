using UnityEngine;

namespace AppreciatorsTcg.Core
{
    public enum AppreciatorsTheme
    {
        Dark,
        Light
    }

    public static class ThemeService
    {
        public static AppreciatorsTheme Current => LocalSaveSystem.LoadTheme();
        public static bool IsDark => Current == AppreciatorsTheme.Dark;
        public static bool ReducedMotion => LocalSaveSystem.LoadReducedMotion();

        public static AppreciatorsTheme Toggle()
        {
            AppreciatorsTheme next = IsDark ? AppreciatorsTheme.Light : AppreciatorsTheme.Dark;
            LocalSaveSystem.SaveTheme(next);
            return next;
        }

        public static Color Surface(Color dark, Color light) => IsDark ? dark : light;
    }
}
