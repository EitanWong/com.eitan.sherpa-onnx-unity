using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Eitan.SherpaONNXUnity.Samples
{
    public sealed class Menu : MonoBehaviour
    {
        [SerializeField] private Button _menuButton;
        [Header("PanelSetup")]
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Button _closeMenuButton;
        [SerializeField] private Button _tamplateButton;
        [SerializeField] private RectTransform _rootOfButtons;
        [Header("Animation Settings")]
        [SerializeField] private float _panelAnimationDuration = 0.3f;
        [SerializeField] private float _buttonAnimationDelay = 0.05f;

        private Coroutine _currentPanelAnimation;
        private Vector2 _menuButtonWorldPosition;


        // Start is called before the first frame update
        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            if (!_menuButton)
            {
                Debug.LogError("The menu button not assign in inspector, please check.");
                return;
            }
            if (!_closeMenuButton)
            {
                Debug.LogError("The close menu button not assign in inspector, please check.");
                return;
            }

            if (!_panel)
            {
                Debug.LogError("The panel component not assign in inspector, please check.");
                return;
            }

            _menuButton.onClick.AddListener(HandleMenuButtonClick);
            _closeMenuButton.onClick.AddListener(HandleCloseMenuButtonClick);

            HandleCloseMenuButtonClick();
            InitializationSceneButtons();
        }


        private void OnDestroy()
        {
            if (_menuButton)
            {
                _menuButton.onClick.RemoveListener(HandleMenuButtonClick);
            }
            if (_closeMenuButton)
            {
                _closeMenuButton.onClick.RemoveListener(HandleCloseMenuButtonClick);
            }
        }

        private void InitializationSceneButtons()
        {
            if (!_tamplateButton || !_rootOfButtons)
            {
                Debug.LogError("The tamplateButton or rootOfButtons not assigned in inspector, please check it again.");
                return;
            }

            StartCoroutine(CreateSceneButtonsWithAnimation());
        }

        private IEnumerator CreateSceneButtonsWithAnimation()
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            _tamplateButton.gameObject.SetActive(false);

            for (int i = 0; i < sceneCount; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                Button newButton = Instantiate(_tamplateButton, _rootOfButtons);
                newButton.transform.localScale = Vector3.zero;
                newButton.gameObject.SetActive(true);

                Text buttonText = newButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = sceneName;
                }

                int sceneIndex = i;
                newButton.onClick.AddListener(() =>
                {
                    StartCoroutine(LoadSceneWithAnimation(sceneIndex));
                });

                AddButtonHoverEffects(newButton);
                StartCoroutine(AnimateButtonIn(newButton.transform));

                yield return new WaitForSeconds(_buttonAnimationDelay);
            }
        }


        #region ButtonCallbackEvent

        private void HandleMenuButtonClick()
        {
            if (_panel)
            {
                if (_currentPanelAnimation != null)
                {
                    StopCoroutine(_currentPanelAnimation);
                }

                _currentPanelAnimation = StartCoroutine(AnimatePanelIn());
            }
        }

        private void HandleCloseMenuButtonClick()
        {
            if (_panel)
            {
                if (_currentPanelAnimation != null)
                {
                    StopCoroutine(_currentPanelAnimation);
                }


                _currentPanelAnimation = StartCoroutine(AnimatePanelOut());
            }
        }
        #endregion

        #region Animation Methods

        private IEnumerator AnimatePanelIn()
        {
            // Calculate menu button position
            Vector3[] corners = new Vector3[4];
            _menuButton.GetComponent<RectTransform>().GetWorldCorners(corners);
            Vector2 buttonCenter = (corners[0] + corners[2]) * 0.5f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _panel.parent as RectTransform, buttonCenter, null, out _menuButtonWorldPosition);

            _menuButton.gameObject.SetActive(false);
            _panel.gameObject.SetActive(true);
            _panel.localScale = Vector3.zero;
            _panel.anchoredPosition = _menuButtonWorldPosition;

            float time = 0f;
            while (time < _panelAnimationDuration)
            {
                time += Time.deltaTime;
                float t = time / _panelAnimationDuration;

                // Simple back ease out: overshoots then settles
                float ease = 1f + 2.7f * Mathf.Pow(t - 1f, 3f) + 1.7f * Mathf.Pow(t - 1f, 2f);

                _panel.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, ease);
                _panel.anchoredPosition = Vector2.Lerp(_menuButtonWorldPosition, Vector2.zero, t);

                yield return null;
            }

            _panel.localScale = Vector3.one;
            _panel.anchoredPosition = Vector2.zero;
            _currentPanelAnimation = null;
        }

        private IEnumerator AnimatePanelOut()
        {
            Vector3 startScale = _panel.localScale;
            Vector2 startPos = _panel.anchoredPosition;
            float duration = _panelAnimationDuration * 0.7f;

            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;

                // Position moves faster (1.3x speed)
                float posT = Mathf.Clamp01(t * 1.3f);
                // Scale uses ease-in for smooth shrinking
                float scaleT = t * t;

                _panel.localScale = Vector3.Lerp(startScale, Vector3.zero, scaleT);
                _panel.anchoredPosition = Vector2.Lerp(startPos, _menuButtonWorldPosition, posT);

                yield return null;
            }

            _panel.localScale = Vector3.zero;
            _panel.anchoredPosition = _menuButtonWorldPosition;
            _panel.gameObject.SetActive(false);
            _menuButton.gameObject.SetActive(true);
            _currentPanelAnimation = null;
        }

        private IEnumerator AnimateButtonIn(Transform buttonTransform)
        {
            float duration = _panelAnimationDuration * 0.5f;
            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;

                // Simple back ease out
                float ease = 1f + 2.7f * Mathf.Pow(t - 1f, 3f) + 1.7f * Mathf.Pow(t - 1f, 2f);
                buttonTransform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, ease);

                yield return null;
            }

            buttonTransform.localScale = Vector3.one;
        }

        private void AddButtonHoverEffects(Button button)
        {
            var eventTrigger = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }


            var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
            };
            pointerEnter.callback.AddListener((data) => StartCoroutine(ScaleButton(button.transform, 1.05f)));
            eventTrigger.triggers.Add(pointerEnter);

            var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
            };
            pointerExit.callback.AddListener((data) => StartCoroutine(ScaleButton(button.transform, 1f)));
            eventTrigger.triggers.Add(pointerExit);

            var pointerDown = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown
            };
            pointerDown.callback.AddListener((data) => StartCoroutine(ScaleButton(button.transform, 0.95f)));
            eventTrigger.triggers.Add(pointerDown);

            var pointerUp = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp
            };
            pointerUp.callback.AddListener((data) => StartCoroutine(ScaleButton(button.transform, 1.05f)));
            eventTrigger.triggers.Add(pointerUp);
        }

        private IEnumerator ScaleButton(Transform buttonTransform, float targetScale)
        {
            Vector3 startScale = buttonTransform.localScale;
            Vector3 endScale = Vector3.one * targetScale;
            float time = 0f;

            while (time < 0.1f)
            {
                time += Time.deltaTime;
                float t = time / 0.1f;
                buttonTransform.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }

            buttonTransform.localScale = endScale;
        }

        private IEnumerator LoadSceneWithAnimation(int sceneIndex)
        {
            yield return StartCoroutine(AnimatePanelOut());
            SceneManager.LoadScene(sceneIndex);
        }

        #endregion

    }
}
