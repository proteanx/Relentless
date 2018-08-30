// Copyright (c) 2018 - Loom Network. All rights reserved.
// https://loomx.io/



using LoomNetwork.CZB.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using LoomNetwork.CZB.Data;
using LoomNetwork.Internal;
using LoomNetwork.CZB.Gameplay;

namespace LoomNetwork.CZB
{
    public class YouWonPopup : IUIPopup
    {
        public GameObject Self
        {
            get { return _selfPage; }
        }

        public static Action OnHidePopupEvent;

        private ILoadObjectsManager _loadObjectsManager;
        private IUIManager _uiManager;
        private GameObject _selfPage,
                            _winTutorialPackObject,
                            _winPackObject;

        private Button _buttonOk;
        private TextMeshProUGUI _message;

        private SpriteRenderer _selectHeroSpriteRenderer;
        private Image _experienceBar;
        private WaitForSeconds _experienceFillWait = new WaitForSeconds(1);
        private TextMeshProUGUI _currentLevel;
        private TextMeshProUGUI _nextLevel;


        //private TextMeshProUGUI _nameHeroText;

        public void Init()
        {
            _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();
            _uiManager = GameClient.Get<IUIManager>();
        }


        public void Dispose()
        {
        }

        public void Hide()
        {
            OnHidePopupEvent?.Invoke();
			GameClient.Get<ICameraManager>().FadeOut(null, 1);

            if (_selfPage == null)
                return;

            _selfPage.SetActive (false);
            GameObject.Destroy (_selfPage);
            _selfPage = null;
		}

        public void SetMainPriority()
        {
        }

        public void Show()
        {
            _selfPage = MonoBehaviour.Instantiate(_loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/UI/Popups/YouWonPopup"));
            _selfPage.transform.SetParent(_uiManager.Canvas3.transform, false);

            _selectHeroSpriteRenderer = _selfPage.transform.Find("Pivot/YouWonPopup/YouWonPanel/SelectHero").GetComponent<SpriteRenderer>();
            _message = _selfPage.transform.Find("Pivot/YouWonPopup/YouWonPanel/UI/Message").GetComponent<TextMeshProUGUI>();
            //_winTutorialPackObject = _selfPage.transform.Find("Pivot/YouWonPopup/YouWonPanel/UI/WinPackTutorial").gameObject;
            //_winPackObject = _selfPage.transform.Find("Pivot/YouWonPopup/YouWonPanel/UI/WinPack").gameObject;
            //_nameHeroText = _selectHeroImage.transform.Find("Text_NameHero").GetComponent<TextMeshProUGUI>();
            _buttonOk = _selfPage.transform.Find("Pivot/YouWonPopup/YouWonPanel/UI/Button_Continue").GetComponent<Button>();
            _buttonOk.onClick.AddListener(OnClickOkButtonEventHandler);
            _buttonOk.gameObject.SetActive(false);
            _experienceBar = _selfPage.transform.Find("Pivot/YouWonPopup/YouWonPanel/UI/ExperienceBar").GetComponent<Image>();
            _currentLevel = _selfPage.transform.Find("Pivot/YouWonPopup/YouWonPanel/UI/CurrentLevel").GetComponent<TextMeshProUGUI>();
            _nextLevel = _selfPage.transform.Find("Pivot/YouWonPopup/YouWonPanel/UI/NextLevel").GetComponent<TextMeshProUGUI>();

            _message.text = "Rewards have been disabled for ver " + BuildMetaInfo.Instance.DisplayVersionName;

            GameClient.Get<ISoundManager>().PlaySound(Enumerators.SoundType.WON_POPUP, Constants.SFX_SOUND_VOLUME, false, false, true);
            GameClient.Get<ICameraManager>().FadeIn(0.8f, 1);
            _selfPage.SetActive(true);

            int playerDeckId = GameClient.Get<IGameplayManager>().PlayerDeckId;

            var dataManager = GameClient.Get<IDataManager>();
            int heroId = dataManager.CachedDecksData.decks.First(d => d.id == playerDeckId).heroId;
            var currentPlayerHero = dataManager.CachedHeroesData.Heroes[heroId];
            string heroName = currentPlayerHero.element.ToString().ToLower();
            _selectHeroSpriteRenderer.sprite = _loadObjectsManager.GetObjectByPath<Sprite>("Images/Heroes/hero_" + heroName.ToLower());
            heroName = Utilites.FirstCharToUpper(heroName);
            //_nameHeroText.text = heroName + " Hero";
 
            // TODO : instead of 1000, should be a value accordint to Level
            // TODO : instead of 400, should be how much player experinece on wining game
            _currentLevel.text = currentPlayerHero.level.ToString();
            _nextLevel.text = (currentPlayerHero.level + 1).ToString();
            var currentExperiencePercentage = (float)currentPlayerHero.experience / 1000;
            _experienceBar.fillAmount = currentExperiencePercentage;
            GameClient.Get<IOverlordManager>().ChangeExperience(currentPlayerHero, 400);
            var updatedExperiencePercetage = (float)currentPlayerHero.experience / 1000; 
         
            //Debug.Log(updatedExperiencePercetage + " , " + currentExperiencePercentage);
            if(updatedExperiencePercetage < currentExperiencePercentage)
                MainApp.Instance.StartCoroutine(FillExperinceBarWithLevelUp(updatedExperiencePercetage, currentPlayerHero.level));
            else
                MainApp.Instance.StartCoroutine(FillExperinceBar(updatedExperiencePercetage));
            
            
            // save to data manager cached hero list
            var index = dataManager.CachedHeroesData.heroes.FindIndex(hero => hero.heroId == heroId);
            if(index != -1) dataManager.CachedHeroesData.heroes[index] = currentPlayerHero;
            //else Debug.LogError(" =========== Hero not foound ======================= ");

            //_winTutorialPackObject.SetActive(GameClient.Get<ITutorialManager>().IsTutorial);
            //_winPackObject.SetActive(!GameClient.Get<ITutorialManager>().IsTutorial);
        }

        private IEnumerator FillExperinceBar(float xpPercentage)
        {
            yield return _experienceFillWait;
            _experienceBar.DOFillAmount(xpPercentage, 1f);
            
            yield return _experienceFillWait;
            _buttonOk.gameObject.SetActive(true);
        }
        
        private IEnumerator FillExperinceBarWithLevelUp(float xpPercentage, int currentLevel)
        {
            yield return _experienceFillWait;
            _experienceBar.DOFillAmount(1, 1f);
            
            // show level up pop up or something
            yield return _experienceFillWait;
            //Debug.Log("====== Show LevelUp Pop Up Or Message ==============");
            _experienceBar.fillAmount = 0f;
            _currentLevel.text = currentLevel.ToString();
            _nextLevel.text = (currentLevel + 1).ToString();
            
            yield return _experienceFillWait;
            _experienceBar.DOFillAmount(xpPercentage, 1f);
            
            yield return _experienceFillWait;
            _buttonOk.gameObject.SetActive(true);
        }

        public void Show(object data)
        {
            Show();
        }

        public void Update()
        {

        }

        private void OnClickOkButtonEventHandler()
        {
            GameClient.Get<ISoundManager>().PlaySound(Common.Enumerators.SoundType.CLICK, Constants.SFX_SOUND_VOLUME, false, false, true);

            GameClient.Get<IMatchManager>().FinishMatch(Enumerators.AppState.DECK_SELECTION);

            GameClient.Get<IDataManager>().SaveCache(Enumerators.CacheDataType.HEROES_DATA);

            _uiManager.HidePopup<YouWonPopup>();
        }

    }
}












