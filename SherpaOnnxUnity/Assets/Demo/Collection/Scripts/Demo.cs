 using UnityEngine;

public sealed class Demo : MonoBehaviour
{

        #region Public Methods
        /// <summary>
        /// 打开GitHub仓库链接 / Open GitHub repository link
        /// </summary>
        public void OpenGithubRepo()
        {
            Application.OpenURL("https://github.com/EitanWong/com.eitan.sherpa-onnx-unity");
        }
        #endregion
}