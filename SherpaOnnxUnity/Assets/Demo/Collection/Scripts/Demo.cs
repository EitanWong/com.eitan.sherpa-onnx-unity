 using UnityEngine;

public sealed class Demo : MonoBehaviour
{
    private void Start()
    {
        //设置模型文件GitHub下载加速代理
        SherpaOnnxUnityAPI.SetGithubProxy("https://gh-proxy.com/");
    }
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