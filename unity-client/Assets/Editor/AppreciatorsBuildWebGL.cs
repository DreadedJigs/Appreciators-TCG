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

            // Public alpha builds may be served through tunnels or static hosts
            // that strip Content-Encoding. Unity's decompression fallback keeps
            // the payload compressed while allowing the browser to unpack it.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

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
                    "Assets/Scenes/PackOpeningScene.unity",
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

            PatchResponsiveWebShell(output);
            if (UnityEngine.Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static void PatchResponsiveWebShell(string output)
        {
            string indexPath = Path.Combine(output, "index.html");
            string stylePath = Path.Combine(output, "TemplateData", "style.css");
            if (!File.Exists(indexPath) || !File.Exists(stylePath))
            {
                throw new FileNotFoundException("WebGL shell files were not generated, so responsive sizing could not be applied.");
            }

            string index = File.ReadAllText(indexPath);
            string buildStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            index = index.Replace(
                "dataUrl: buildUrl + \"/WebGL.data.unityweb\",",
                "dataUrl: buildUrl + \"/WebGL.data.unityweb?v=" + buildStamp + "\",");
            index = index.Replace(
                "frameworkUrl: buildUrl + \"/WebGL.framework.js.unityweb\",",
                "frameworkUrl: buildUrl + \"/WebGL.framework.js.unityweb?v=" + buildStamp + "\",");
            index = index.Replace(
                "codeUrl: buildUrl + \"/WebGL.wasm.unityweb\",",
                "codeUrl: buildUrl + \"/WebGL.wasm.unityweb?v=" + buildStamp + "\",");
            index = index.Replace(
                "productVersion: \"1.0\",",
                "productVersion: \"alpha-" + buildStamp + "\",");
            index = index.Replace(
                "var config = {",
                "var config = {\n        devicePixelRatio: Math.min(window.devicePixelRatio || 1, 2),");
            index = index.Replace(
                "canvas.style.width = \"1920px\";\n        canvas.style.height = \"1080px\";",
                "canvas.style.width = \"100%\";\n        canvas.style.height = \"100%\";");
            File.WriteAllText(indexPath, index);

            string responsiveCss = @"
html, body { width: 100%; height: 100%; min-height: 100dvh; overflow: hidden; background: #050515; }
#unity-container.unity-desktop {
  width: min(100vw, calc(100vh * 16 / 9));
  height: min(100vh, calc(100vw * 9 / 16));
}
#unity-container.unity-desktop #unity-canvas { width: 100%; height: 100%; }
#unity-container.unity-desktop #unity-footer { display: none; }
#unity-container.unity-mobile {
  position: fixed;
  inset: 0;
  width: 100vw;
  height: 100vh;
  height: 100dvh;
}
#unity-container.unity-mobile #unity-canvas {
  width: 100%;
  height: 100%;
  display: block;
}
@media (orientation: landscape) and (max-height: 900px) {
  html, body, #unity-container, #unity-container.unity-desktop, #unity-container.unity-mobile {
    position: fixed !important;
    inset: 0 !important;
    width: 100vw !important;
    height: 100dvh !important;
    max-width: none !important;
    max-height: none !important;
    margin: 0 !important;
    transform: none !important;
  }
  #unity-canvas { width: 100% !important; height: 100% !important; display: block; }
  #unity-footer { display: none !important; }
}
";
            File.AppendAllText(stylePath, responsiveCss);
            string fullscreenScript = @"
<script>
(function () {
  function isMobileLandscape() { return navigator.maxTouchPoints > 0 && matchMedia('(orientation: landscape) and (max-height: 900px)').matches; }
  function enterGameFullscreen() {
    if (!isMobileLandscape() || document.fullscreenElement) return;
    var root = document.documentElement;
    var request = root.requestFullscreen || root.webkitRequestFullscreen;
    if (!request) return;
    Promise.resolve(request.call(root)).then(function () {
      if (screen.orientation && screen.orientation.lock) screen.orientation.lock('landscape').catch(function () {});
    }).catch(function () {});
  }
  document.addEventListener('pointerup', enterGameFullscreen, { passive: true });
})();
</script>
";
            index = File.ReadAllText(indexPath);
            index = index.Replace("</body>", fullscreenScript + "</body>");
            File.WriteAllText(indexPath, index);
            UnityEngine.Debug.Log("Applied responsive 16:9 and 4K-ready sizing to the WebGL shell.");
        }
    }
}
#endif
