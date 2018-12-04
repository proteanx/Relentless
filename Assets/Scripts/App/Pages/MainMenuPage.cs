using System;
using Loom.ZombieBattleground.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Loom.ZombieBattleground
{
    public class MainMenuPage : IUIElement
    {
        private IUIManager _uiManager;

        private ILoadObjectsManager _loadObjectsManager;

        private IAppStateManager _stateManager;

        private ISoundManager _soundManager;

        private IPlayerManager _playerManager;

        private IDataManager _dataManager;

        private ILocalizationManager _localizationManager;

        private GameObject _selfPage;

        private MenuButtonNoGlow _buttonArmy;

        private Button _buttonPlay, _buttonSettings, _buttonCredits;

        private ButtonShiftingContent _buttonBuy, _buttonOpen;

        private TextMeshProUGUI _packsCount;

        private TextMeshProUGUI _armyTextMesh, _armyHoverTextMesh, _armyDownTextMesh;

        private Animator _logoAnimator;

        public void Init()
        {
            _uiManager = GameClient.Get<IUIManager>();
            _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();
            _stateManager = GameClient.Get<IAppStateManager>();
            _soundManager = GameClient.Get<ISoundManager>();
            _playerManager = GameClient.Get<IPlayerManager>();
            _dataManager = GameClient.Get<IDataManager>();
            _localizationManager = GameClient.Get<ILocalizationManager>();

            _localizationManager.LanguageWasChangedEvent += LanguageWasChangedEventHandler;
        }

        public void Update()
        {
        }

        public void Show()
        {
            _selfPage = Object.Instantiate(
                _loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/UI/Pages/MainMenuPage"));
            _selfPage.transform.SetParent(_uiManager.Canvas.transform, false);

            _buttonPlay = _selfPage.transform.Find("Button_Play").GetComponent<Button>();

            _buttonArmy = _selfPage.transform.Find("Button_Army").GetComponent<MenuButtonNoGlow>();
            _buttonCredits = _selfPage.transform.Find("Button_Credits").GetComponent<Button>();
            _buttonBuy = _selfPage.transform.Find("Button_Shop").GetComponent<ButtonShiftingContent>();
            _buttonOpen = _selfPage.transform.Find("Button_OpenPacks").GetComponent<ButtonShiftingContent>();
            _packsCount = _selfPage.transform.Find("Button_OpenPacks/Count").GetComponent<TextMeshProUGUI>();
            _buttonSettings = _selfPage.transform.Find("Button_Settings").GetComponent<Button>();
            _armyTextMesh = _buttonArmy.transform.Find("Button/ButtonText").GetComponent<TextMeshProUGUI>();
            _armyHoverTextMesh = _buttonArmy.transform.Find("Hover/HoverText").GetComponent<TextMeshProUGUI>();
            _armyDownTextMesh = _buttonArmy.transform.Find("Down/DownText").GetComponent<TextMeshProUGUI>();

            _logoAnimator = _selfPage.transform.Find("Logo").GetComponent<Animator>();

            _buttonPlay.onClick.AddListener(OnClickPlay);
            _buttonArmy.Clicked.AddListener(OnClickCollection);
            _buttonBuy.onClick.AddListener(BuyButtonHandler);
            _buttonOpen.onClick.AddListener(OpenButtonHandler);

            _buttonCredits.onClick.AddListener(CreditsButtonOnClickHandler);
            _buttonSettings.onClick.AddListener(SettingsButtonOnClickHandler);

            _buttonArmy.Interactable = true;

            _packsCount.text = _playerManager.LocalUser.PacksCount <= 99 ?
                _playerManager.LocalUser.PacksCount.ToString() :
                "99";

            if (_logoAnimator.gameObject.activeInHierarchy)
            {
                _logoAnimator.SetBool("LogoShow", true);
            }

            if (!_dataManager.CachedUserLocalData.AgreedTerms)
            {
                _uiManager.DrawPopup<TermsPopup>();
            }

            UpdateLocalization();
        }

        public void Hide()
        {
            if (_selfPage == null)
                return;

            _selfPage.SetActive(false);
            Object.Destroy(_selfPage);
            _selfPage = null;
        }

        public void Dispose()
        {
        }

        private void LanguageWasChangedEventHandler(Enumerators.Language obj)
        {
            UpdateLocalization();
        }

        private void UpdateLocalization()
        {
            if (_selfPage == null)
                return;

            _armyTextMesh.text = _localizationManager.GetUITranslation(LocalizationKeys.ArmyText.ToString());
            _armyHoverTextMesh.text = _localizationManager.GetUITranslation(LocalizationKeys.ArmyText.ToString());
            _armyDownTextMesh.text = _localizationManager.GetUITranslation(LocalizationKeys.ArmyText.ToString());
            // TODO : right now all text are images.
            /*_playText.text = _localizationManager.GetUITranslation(LocalizationKeys.PlayText.ToString());
            _shopText.text = _localizationManager.GetUITranslation(LocalizationKeys.ShopText.ToString());
            _openPacksText.text = _localizationManager.GetUITranslation(LocalizationKeys.OpenPacksText.ToString());*/
        }

        private void OpenAlertDialog(string msg)
        {
            _uiManager.DrawPopup<WarningPopup>(msg);
        }

        #region Buttons Handlers

        private void OnClickPlay()
        {
            _soundManager.PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);
            if (GameClient.Get<IDataManager>().CachedUserLocalData.Tutorial)
            {
                _uiManager.GetPage<GameplayPage>().CurrentDeckId = 0;

                GameClient.Get<IMatchManager>().FindMatch(Enumerators.MatchType.LOCAL);
            }
            else
            {
                _stateManager.ChangeAppState(Enumerators.AppState.PlaySelection);
            }
        }

        private void OnClickCollection()
        {
            _soundManager.PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);
            _stateManager.ChangeAppState(Enumerators.AppState.ARMY);
        }

        private void BuyButtonHandler()
        {
            _soundManager.PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);
            _stateManager.ChangeAppState(Enumerators.AppState.SHOP);
        }

        private void CreditsButtonOnClickHandler()
        {
            _soundManager.PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);
            _stateManager.ChangeAppState(Enumerators.AppState.CREDITS);
        }

        private void OpenButtonHandler()
        {
            _soundManager.PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);
            _stateManager.ChangeAppState(Enumerators.AppState.PACK_OPENER);
        }

        private void SettingsButtonOnClickHandler()
        {
            _uiManager.DrawPopup<SettingsPopup>(true);
        }

        #endregion

    }
}
