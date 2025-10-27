using UnityEngine;

namespace Eitan.SherpaOnnxUnity.Samples
{
    [CreateAssetMenu(fileName = "New ZeroShotPrompt", menuName = "SherpaOnnx/Create ZeroShotPrompt")]
    public class ZeroShotPrompt : ScriptableObject
    {
        [SerializeField] private Texture2D icon;
        [SerializeField] private AudioClip promptAudio;
        [SerializeField] private TextAsset promptText;

        #region Properities
        public Texture2D Icon => icon;
        public AudioClip PromptAudio => promptAudio;
        public TextAsset PromptText => promptText;

        #endregion
    }
}
