using UnityEngine;
using UnityEngine.UI;

public sealed class Demo : MonoBehaviour
{
    [SerializeField] private Text versionText;

    private void Start()
    {
        if (versionText != null)
        {
            versionText.text = $"v{SherpaONNXUnityAPI.SherpaONNXLibVersion}";
        }
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
