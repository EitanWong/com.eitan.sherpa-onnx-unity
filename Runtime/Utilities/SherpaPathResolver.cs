using Eitan.SherpaONNXUnity.Runtime.Constants;

namespace Eitan.SherpaONNXUnity.Runtime.Utilities
{

    internal static class SherpaPathResolver
    {

        public static string GetModelRootPath(string modelID)
        {
            //check the modelId if it's Empty

            if (string.IsNullOrEmpty(modelID))
            {
                throw new System.Exception("The modelID can't be Null or Empty");
            }

            var moduleType = SherpaUtils.Model.GetModuleTypeByModelId(modelID);
            return System.IO.Path.Combine(GetModuleRootPath(moduleType), modelID);
        }

        public static string GetModuleRootPath(SherpaONNXModuleType moduleType)
        {
            var ModuleName = System.Text.RegularExpressions.Regex.Replace(moduleType.ToString(), @"([a-z])([A-Z])", "$1-$2").ToLower();

            var modelPathFolder = System.IO.Path.Combine(SherpaONNXConstants.RootDirectoryName, SherpaONNXConstants.ModelRootDirectoryName);

#if UNITY_EDITOR
            return System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, modelPathFolder, ModuleName);
#elif UNITY_ANDROID
            return System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, modelPathFolder, ModuleName);
#elif UNITY_IOS
            return System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, modelPathFolder,ModuleName);
#else
            return System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, modelPathFolder,ModuleName);
#endif
        }



        public static string GetModelFilePath(string modelId, string modelFile)
        {
            if (string.IsNullOrEmpty(modelFile))
            {
                throw new System.Exception("modelFile can't be Null or Empty");
            }
            var modelFolderPath = GetModelRootPath(modelId);
            if (string.IsNullOrEmpty(modelFolderPath))
            {
                throw new System.Exception("model Folder can't found");
            }

            return System.IO.Path.Combine(modelFolderPath, modelFile);

        }

    }

}
