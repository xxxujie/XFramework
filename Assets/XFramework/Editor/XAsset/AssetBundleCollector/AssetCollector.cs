using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XFramework.Utils;

namespace XFramework.XAsset
{
    public sealed class AssetCollector
    {
        private readonly List<string> _allFolders = new();

        private UnityEngine.Object _target;
        private bool _recursiveCollect;
        private bool _IsDropDownExpanded;

        public AssetCollector() : this(null, true)
        {
        }

        public AssetCollector(UnityEngine.Object target) : this(target, true)
        {
        }

        public AssetCollector(UnityEngine.Object target, bool recursiveCollect)
        {
            _target = target;
            _recursiveCollect = recursiveCollect;
            _IsDropDownExpanded = false;
        }

        public UnityEngine.Object Target
        {
            get => _target;
            set => _target = value;
        }

        public string TargetPath
        {
            get
            {
                if (_target == null)
                {
                    return null;
                }
                return AssetDatabase.GetAssetPath(_target);
            }
        }

        public bool IsDropDownExpanded
        {
            get => _IsDropDownExpanded;
            set => _IsDropDownExpanded = value;
        }

        public bool IsFolder
        {
            get
            {
                if (Directory.Exists(TargetPath))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public string[] GetAllAssetPaths()
        {
            if (!IsFolder)
            {
                return new string[] { TargetPath };
            }

            string[] allAssetGUIDs;
            if (_recursiveCollect)
            {
                UpdateAllFoldersRecursively(TargetPath);
                allAssetGUIDs = AssetDatabase.FindAssets(string.Empty, _allFolders.ToArray());
            }
            else
            {
                allAssetGUIDs = AssetDatabase.FindAssets(string.Empty, new string[] { TargetPath });
            }

            var result = new List<string>();
            foreach (string guid in allAssetGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith(".cs"))
                {
                    continue;
                }
                result.Add(assetPath);
            }

            Log.Debug($"Get {result.Count} assets in {TargetPath}");

            return result.ToArray();
        }

        private void UpdateAllFoldersRecursively(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            _allFolders.Add(folder);
            string[] subFolders = AssetDatabase.GetSubFolders(folder);
            foreach (string subFolder in subFolders)
            {
                UpdateAllFoldersRecursively(subFolder);
            }
        }
    }
}