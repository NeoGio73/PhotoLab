using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PhotoLab.EditorTools
{
    /// <summary>
    /// Makes the browser version of the photosynthesis lab. Menu: Photo Lab > Build for the Browser.
    /// The finished site lands in Builds/PhotoLab next to the Assets folder. The build leaves out the
    /// other labs' code: their assemblies only compile when PHOTO_LAB_BUILD is not defined.
    /// </summary>
    public static class PhotoWebBuild
    {
        public const string OutputFolder = "Builds/PhotoLab";
        const string ResultFile = "Builds/photo_build_result.txt";

        [MenuItem("Photo Lab/Build for the Browser")]
        public static void Build()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.Combine(projectRoot, OutputFolder);
            string resultPath = Path.Combine(projectRoot, ResultFile);
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            File.WriteAllText(resultPath, "RUNNING");
            try
            {
                ConfigurePlayer();
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { PhotoSceneBuilder.ScenePath },
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                    extraScriptingDefines = new[] { "PHOTO_LAB_BUILD" }
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                string text = summary.result + "\nerrors=" + summary.totalErrors + "\nwarnings=" + summary.totalWarnings +
                    "\nbytes=" + summary.totalSize + "\nseconds=" + summary.totalTime.TotalSeconds.ToString("F0") + "\noutput=" + summary.outputPath;
                foreach (BuildStep step in report.steps)
                    foreach (BuildStepMessage message in step.messages)
                        if (message.type == LogType.Error || message.type == LogType.Exception) text += "\nERROR: " + message.content;
                File.WriteAllText(resultPath, text);
                Debug.Log("Photo lab: browser build " + summary.result + " -> " + output);
            }
            catch (System.Exception e)
            {
                File.WriteAllText(resultPath, "EXCEPTION\n" + e);
                throw;
            }
        }

        /// <summary>Player settings a static web host (GitHub Pages) needs: gzip with the decompression fallback, since Pages sends no Content-Encoding header.</summary>
        public static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Photosynthesis Lab";
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.WebGL.template = "PROJECT:PhotoLab";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            AssetDatabase.SaveAssets();
        }
    }
}
