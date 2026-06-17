#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace AppreciatorsTcg.EditorTools
{
    public static class AppreciatorsBuildWebGL
    {
        public static void Build()
        {
            string output = "Builds/WebGL";
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[]
                {
                    "Assets/Scenes/Main.unity",
                    "Assets/Scenes/LoginScene.unity",
                    "Assets/Scenes/MainMenuScene.unity",
                    "Assets/Scenes/CollectionScene.unity",
                    "Assets/Scenes/DeckBuilderScene.unity",
                    "Assets/Scenes/InviteMatchScene.unity",
                    "Assets/Scenes/MatchScene.unity",
                    "Assets/Scenes/ResultsScene.unity",
                    "Assets/Scenes/Web3MockScene.unity"
                },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception("WebGL build failed with result: " + report.summary.result);
            }
        }
    }
}
#endif
