// Licensed under the MIT License. See LICENSE in the project root for license information.

using Eitan.SherpaOnnxUnity.Runtime.Utilities;
using NUnit.Framework;

namespace EitanWong.SherpaOnnxUnity.Tests
{
    internal class ModelTypeDetectionTest
    {
        [Test]
        public void TestModelTypePasses()
        {
            // A Test behaves as an ordinary method
            // Use the Assert class to test conditions
            var modelID = "sherpa-onnx-paraformer-zh-small-2024-03-09";
            var isOnlineModel = SherpaUtils.Model.IsOnlineModel(modelID);
            var modelType = SherpaUtils.Model.GetSpeechRecognitionModelType(modelID);
            UnityEngine.Debug.Log(isOnlineModel);
            
            UnityEngine.Debug.Log(modelType);
            
        }
    }
}
