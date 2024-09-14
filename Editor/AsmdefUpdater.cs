using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace devolfer.Sound
{
    internal static class AsmdefUpdater
    {
        private const string PackagePath = "Packages/com.devolfer.sound/";
        private const string PackageIdentifier = "devolfer.Sound.Runtime";

        private const string UniTaskTypeName = "Cysharp.Threading.Tasks.UniTask, UniTask";
        private const string UniTaskReferenceName = "UniTask";

        private static readonly AsmdefData.VersionDefine s_uniTaskVersionDefine = new()
            { name = "com.cysharp.unitask", expression = "", define = "UNITASK_INCLUDED" };

        // [InitializeOnLoadMethod]
        public static void UpdateAsmdef()
        {
            UpdateAsmdefDependency(PackageIdentifier, UniTaskTypeName, UniTaskReferenceName, s_uniTaskVersionDefine);
        }

        private static void UpdateAsmdefDependency(string packageIdentifier,
                                                   string dependencyPackageTypeName,
                                                   string dependencyPackageReferenceName,
                                                   AsmdefData.VersionDefine dependencyVersionDefine)
        {
            string asmdefPath = GetAsmdefPath(packageIdentifier);
            bool dependencyPackageInstalled = Type.GetType(dependencyPackageTypeName) != null;
            bool asmdefContainsUniTaskReference = AsmdefContainsReference(asmdefPath, dependencyPackageReferenceName);

            if (dependencyPackageInstalled)
            {
                if (!asmdefContainsUniTaskReference)
                {
                    AddAsmdefReference(asmdefPath, dependencyPackageReferenceName, dependencyVersionDefine);
                }
            }
            else
            {
                if (asmdefContainsUniTaskReference)
                {
                    RemoveAsmdefReference(asmdefPath, dependencyPackageReferenceName, dependencyVersionDefine);
                }
            }
        }

        private static string GetAsmdefPath(string packageIdentifier)
        {
            try
            {
                foreach (string file in Directory.GetFiles(
                             Path.GetFullPath(PackagePath),
                             "*.asmdef",
                             SearchOption.AllDirectories))
                {
                    if (file.Contains(packageIdentifier)) return file;
                }
            }
            catch (Exception e)
            {
                // ignored
            }

            Debug.LogError("Could not find the .asmdef file in Packages or Assets folder.");

            return null;
        }

        private static bool AsmdefContainsReference(string asmdefPath, string referenceName)
        {
            if (string.IsNullOrEmpty(asmdefPath) || !File.Exists(asmdefPath)) return false;

            return File.ReadAllText(asmdefPath).Contains(referenceName);
        }

        private static void AddAsmdefReference(string asmdefPath,
                                               string referenceName,
                                               AsmdefData.VersionDefine versionDefine)
        {
            if (string.IsNullOrEmpty(asmdefPath) || !File.Exists(asmdefPath)) return;

            AsmdefData asmdefData = JsonUtility.FromJson<AsmdefData>(File.ReadAllText(asmdefPath));

            asmdefData.references ??= new string[1];
            if (!asmdefData.references.Contains(referenceName))
            {
                ArrayUtility.Add(ref asmdefData.references, referenceName);
                asmdefData.versionDefines ??= new AsmdefData.VersionDefine[1];
                ArrayUtility.Add(ref asmdefData.versionDefines, versionDefine);
            }

            File.WriteAllText(asmdefPath, JsonUtility.ToJson(asmdefData, true));

            AssetDatabase.ImportAsset(asmdefPath);
        }

        private static void RemoveAsmdefReference(string asmdefPath,
                                                  string referenceName,
                                                  AsmdefData.VersionDefine versionDefine)
        {
            if (string.IsNullOrEmpty(asmdefPath) || !File.Exists(asmdefPath)) return;

            AsmdefData asmdefData = JsonUtility.FromJson<AsmdefData>(File.ReadAllText(asmdefPath));

            if (asmdefData.references.Contains(referenceName))
            {
                ArrayUtility.Remove(ref asmdefData.references, referenceName);

                foreach (AsmdefData.VersionDefine vD in asmdefData.versionDefines)
                {
                    if (vD == versionDefine) ArrayUtility.Remove(ref asmdefData.versionDefines, vD);
                }
            }

            File.WriteAllText(asmdefPath, JsonUtility.ToJson(asmdefData, true));

            AssetDatabase.ImportAsset(asmdefPath);
        }

        [Serializable]
        private class AsmdefData
        {
            public string name;
            public string rootNamespace;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
            public bool allowUnsafeCode;
            public bool overrideReferences;
            public string[] precompiledReferences;
            public bool autoReferenced;
            public string[] defineConstraints;
            public VersionDefine[] versionDefines;
            public bool noEngineReferences;

            [Serializable]
            public class VersionDefine
            {
                public string name;
                public string expression;
                public string define;

                public static bool operator ==(VersionDefine vd1, VersionDefine vd2)
                {
                    if (ReferenceEquals(vd1, null) && ReferenceEquals(vd2, null)) return true;
                    if (ReferenceEquals(vd1, null) || ReferenceEquals(vd2, null)) return false;

                    return vd1.define == vd2.define;
                }

                public static bool operator !=(VersionDefine vd1, VersionDefine vd2) => !(vd1 == vd2);

                public override bool Equals(object obj)
                {
                    if (obj is VersionDefine other) return define == other.define;

                    return false;
                }

                public override int GetHashCode() => define != null ? define.GetHashCode() : 0;
            }
        }
    }
}