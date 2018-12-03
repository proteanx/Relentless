using System.Collections;
using System.Linq;
using DG.Tweening;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using Loom.ZombieBattleground.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Loom.ZombieBattleground
{
    public class YouWonPopup : IUIPopup
    {
        private readonly WaitForSeconds _experienceFillWait = new WaitForSeconds(1);

        private ILoadObjectsManager _loadObjectsManager;

        private IUIManager _uiManager;

        private IOverlordExperienceManager _overlordManager;

        private IDataManager _dataManager;

        private ISoundManager _soundManager;

        private IGameplayManager _gameplayManager;

        private ITutorialManager _tutorialManager;

        private IMatchManager _matchManager;

        private ICameraManager _cameraManager;

        private Button _buttonOk;

        private TextMeshProUGUI _message;

        private SpriteRenderer _selectHeroSpriteRenderer;

        private Image _experienceBar;

        private TextMeshProUGUI _currentLevel;

        private TextMeshProUGUI _nextLevel;

        public GameObject Self { get; private set; }

        private Hero _currentPlayerHero;

        private bool _isLevelUp;

        public void Init()
        {
            _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();
            _uiManager = GameClient.Get<IUIManager>();
            _overlordManager = GameClient.Get<IOverlordExperienceManager>();
            _dataManager = GameClient.Get<IDataManager>();
            _soundManager = GameClient.Get<ISoundManager>();
            _gameplayManager = GameClient.Get<IGameplayManager>();
            _tutorialManager = GameClient.Get<ITutorialManager>();
            _matchManager = GameClient.Get<IMatchManager>();
            _cameraManager = GameClient.Get<ICameraManager>();
        }

        public void Dispose()
        {
        }

        public void Hide()
        {
            _cameraManager.FadeOut(null, 1);

            if (Self == null)
                return;

            Self.SetActive(false);
            Object.Destroy(Self);
            Self = null;
        }

        public void SetMainPriority()
        {
        }

        public void Show()
        {
            if (Self != null)
                return;

            Self = Object.Instantiate(_loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/UI/Popups/YouWonPopup"));
            Self.transform.SetParent(_uiManager.Canvas3.transform, false);

            _selectHeroSpriteRenderer = Self.transform.Find("Pivot/YouWonPopup/YouWonPanel/SelectHero")
                .GetComponent<SpriteRenderer>();
            _message = Self.transform.Find("Pivot/YouWonPopup/YouWonPanel/UI/Message").GetComponent<TextMeshProUGUI>();

            _buttonOk = Self.transform.Find("Pivot/YouWonPopup/YouWonPanel/UI/Button_Continue").GetComponent<Button>();
            _buttonOk.onClick.AddListener(OnClickOkButtonEventHandler);
            _buttonOk.gameObject.SetActive(false);
            _experienceBar = Self.transform.Find("Pivot/YouWonPopup/YouWonPanel/UI/ExperienceBar")
                .GetComponent<Image>();
            _currentLevel = Self.transform.Find("Pivot/YouWonPopup/YouWonPanel/UI/CurrentLevel")
                .GetComponent<TextMeshProUGUI>();
            _nextLevel = Self.transform.Find("Pivot/YouWonPopup/YouWonPanel/UI/NextLevel")
                .GetComponent<TextMeshProUGUI>();

            _message.text = "Rewards have been disabled for ver " + BuildMetaInfo.Instance.DisplayVersionName;

            _soundManager.PlaySound(Enumerators.SoundType.WON_POPUP, Constants.SfxSoundVolume, false, false, true);

            _cameraManager.FadeIn(0.8f, 1);

            Self.SetActive(true);

            int heroId = _gameplayManager.IsTutorial
                ? _tutorialManager.CurrentTutorial.SpecificBattlegroundInfo.PlayerInfo.HeroId : _gameplayManager.CurrentPlayerDeck.HeroId;

            _currentPlayerHero = _dataManager.CachedHeroesData.Heroes[heroId];
            string heroName = _currentPlayerHero.HeroElement.ToString().ToLowerInvariant();

            _selectHeroSpriteRenderer.sprite =
                _loadObjectsManager.GetObjectByPath<Sprite>("Images/Heroes/hero_" + heroName.ToLowerInvariant());

            _overlordManager.ApplyExperienceFromMatch(_currentPlayerHero);

            _currentLevel.text = (_overlordManager.MatchExperienceInfo.LevelAtBegin).ToString();
            _nextLevel.text = (_overlordManager.MatchExperienceInfo.LevelAtBegin + 1).ToString();

            _isLevelUp = false;

            float currentExperiencePercentage = (float)_overlordManager.MatchExperienceInfo.ExperienceAtBegin /
                                                _overlordManager.GetRequiredExperienceForNewLevel(_currentPlayerHero);
            _experienceBar.fillAmount = currentExperiencePercentage;

            FillingExperinceBar();
        }

        private void FillingExperinceBar()
        {
            if (_currentPlayerHero.Level > _overlordManager.MatchExperienceInfo.LevelAtBegin)
            {
                MainApp.Instance.StartCoroutine(FillExperinceBarWithLevelUp(_currentPlayerHero.Level));
            }
            else if (_currentPlayerHero.Experience > _overlordManager.MatchExperienceInfo.ExperienceAtBegin)
            {
                float updatedExperiencePercetage = (float)_currentPlayerHero.Experience
                    / _overlordManager.GetRequiredExperienceForNewLevel(_currentPlayerHero);

                MainApp.Instance.StartCoroutine(FillExperinceBar(updatedExperiencePercetage));
            }
            else
            {
                _buttonOk.gameObject.SetActive(true);
            }
        }

        public void Show(object data)
        {
            Show();
        }

        public void Update()
        {
        }

        private IEnumerator FillExperinceBar(float xpPercentage)
        {
            yield return _experienceFillWait;
            _experienceBar.DOFillAmount(xpPercentage, 1f);

            yield return _experienceFillWait;
            _buttonOk.gameObject.SetActive(true);

            if (_isLevelUp)
            {
                _uiManager.DrawPopup<LevelUpPopup>();
            }
        }

        private IEnumerator FillExperinceBarWithLevelUp(int currentLevel)
        {
            yield return _experienceFillWait;
            _experienceBar.DOFillAmount(1, 1f);

            yield return _experienceFillWait;

            _overlordManager.MatchExperienceInfo.LevelAtBegin++;

            _experienceBar.fillAmount = 0f;
            _currentLevel.text = _overlordManager.MatchExperienceInfo.LevelAtBegin.ToString();
            _nextLevel.text = (_overlordManager.MatchExperienceInfo.LevelAtBegin + 1).ToString();

            _isLevelUp = true;

            FillingExperinceBar();
        }

        private void OnClickOkButtonEventHandler()
        {
            _soundManager.PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);

            _uiManager.HidePopup<YouWonPopup>();

            if (_gameplayManager.IsTutorial)
            {
                _matchManager.FinishMatch(Enumerators.AppState.PlaySelection);
            }
            else
            {
                _matchManager.FinishMatch(Enumerators.AppState.HordeSelection);
            }
        }
    }
}
