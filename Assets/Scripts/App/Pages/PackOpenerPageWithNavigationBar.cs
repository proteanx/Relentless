using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DG.Tweening;
using log4net;
using Loom.Client;
using Loom.ZombieBattleground.BackendCommunication;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using Loom.ZombieBattleground.Gameplay;
using Loom.ZombieBattleground.Helpers;
using Loom.ZombieBattleground.Iap;
using OneOf;
using OneOf.Types;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using CardKey = Loom.ZombieBattleground.Data.CardKey;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Loom.ZombieBattleground
{
    public class PackOpenerPageWithNavigationBar : IUIElement
    {
        private const float FadeDuration = 1f;
        private const float CardFlipAnimationDuration = 0.3f;
        private const float BoardCardViewOpenedCardScale = 0.26f;
        private const float PackScrollAnimationDuration = 0.3f;
        private const float PackWidth = 584f;
        private const float CardsFlyOutAnimationLength = 0.21f;
        private const float GooFillMaxValue = 4f;
        private const float GooFillDecreaseMultiplier = 9f;
        private const float PreDimAlpha = 0.5f;
        private const float DimAlpha = 1f;

        private static readonly Vector3 CardHidePosition = new Vector3(0, -11.5f, 0);

        private static readonly ILog Log = Logging.GetLog(nameof(PackOpenerPageWithNavigationBar));

        private static readonly string[] PackOpenAnimationPrefabPaths =
        {
            "Prefabs/UI/Pages/PackOpener/PackOpenerAnimationOne",
            "Prefabs/UI/Pages/PackOpener/PackOpenerAnimationTwo",
            "Prefabs/UI/Pages/PackOpener/PackOpenerAnimationThree",
            "Prefabs/UI/Pages/PackOpener/PackOpenerAnimationFour",
        };

        private readonly List<PackObject> _packObjects = new List<PackObject>();

        private readonly List<PackObject> _fakeLeftPackObjects = new List<PackObject>();

        private readonly List<PackObject> _fakeRightPackObjects = new List<PackObject>();

        private readonly List<OpenedPackCard> _openedCards = new List<OpenedPackCard>();

        private IUIManager _uiManager;

        private ILoadObjectsManager _loadObjectsManager;

        private PlasmachainBackendFacade _plasmaChainBackendFacade;

        private BackendDataControlMediator _backendDataControlMediator;

        private ITutorialManager _tutorialManager;

        private IDataManager _dataManager;

        private CardsController _cardsController;

        private Material _grayScaleMaterial;

        private GameObject[] _packOpenAnimationPrefabs;

        private PackOpeningAnimationData[] _packOpenAnimationsData;

        private RectTransform _packOpenAnimationWrapperTransform;

        private GameObject _selfPage;

        private RectTransform _wrapperTransform;

        private GameObject _openedPackPanel;

        private CanvasGroup _openedPackPanelCanvasGroup;

        private CanvasGroup _openedPackPanelBottomButtonsCanvasGroup;

        private Button _openedPackPanelCloseButton;

        private Button _openedPackPanelOpenNextPackButton;

        private Button _buyMorePacksButton;

        private GameObject _packObjectsRoot;

        private RectTransform _packObjectsRootRectTransform;

        private Animator _gooFillingAnimator;

        private HorizontalLayoutGroup _packObjectsRootHorizontalLayoutGroup;

        private TextMeshProUGUI _currentPackTypeText;

        private TextMeshProUGUI _currentPackTypeAmountText;

        private Button _openButton;

        private UIBehaviourEventNotifier _openButtonEventNotifier;

        private Button _scrollLeftButton;

        private Button _scrollRightButton;

        private Enumerators.MarketplaceCardPackType? _selectedPackType;

        private PackOpenerControllerBase _controller;

        private CardInfoPopupHandler _cardInfoPopupHandler;

        private Sequence _currentPackScrollSequence;

        private GameObject _openingPackAnimationObject;

        private bool _isOpenButtonBeingHeld;

        private float _openButtonHoldTimeCounter;

        private int _lastPackOpenAnimationIndex;

        #region IUIElement

        public void Init()
        {
            _uiManager = GameClient.Get<IUIManager>();
            _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();
            _plasmaChainBackendFacade = GameClient.Get<PlasmachainBackendFacade>();
            _backendDataControlMediator = GameClient.Get<BackendDataControlMediator>();
            _tutorialManager = GameClient.Get<ITutorialManager>();
            _dataManager = GameClient.Get<IDataManager>();
            _cardsController = GameClient.Get<IGameplayManager>().GetController<CardsController>();

            _grayScaleMaterial = _loadObjectsManager.GetObjectByPath<Material>("Materials/UI-Default-Grayscale");
            _packOpenAnimationPrefabs = _loadObjectsManager.GetObjectsByPath<GameObject>(PackOpenAnimationPrefabPaths);
            _packOpenAnimationsData =
                _packOpenAnimationPrefabs
                    .Select(prefab => prefab.GetComponent<PackOpeningAnimationData>())
                    .ToArray();
        }

        public async void Show()
        {
            _selectedPackType = null;

            if (!_tutorialManager.IsTutorial)
            {
                _controller = new NormalPackOpenerController();
            }
            else
            {
                _controller = new TutorialPackOpenerController();
            }

            _controller = new FakePackOpenerController();

            _cardInfoPopupHandler = new CardInfoPopupHandler();
            _cardInfoPopupHandler.Init();

            _selfPage = Object.Instantiate(
                _loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/UI/Pages/PackOpener/PackOpenerPageWithNavigationBar"),
                _uiManager.Canvas.transform,
                false);

            _openedPackPanel = _selfPage.transform.Find("OpenedPackPanel").gameObject;
            _openedPackPanelCanvasGroup = _openedPackPanel.GetComponent<CanvasGroup>();
            _openedPackPanelBottomButtonsCanvasGroup = _openedPackPanel.transform.Find("BottomButtons").GetComponent<CanvasGroup>();
            _openedPackPanelCloseButton = _openedPackPanel.transform.Find("BottomButtons/Button_ClosePackOpener").GetComponent<Button>();
            _openedPackPanelOpenNextPackButton = _openedPackPanel.transform.Find("BottomButtons/Button_OpenNextPack").GetComponent<Button>();

            _wrapperTransform = _selfPage.transform.Find("PackOpenerWrapper").GetComponent<RectTransform>();
            _buyMorePacksButton = _wrapperTransform.Find("PromoPanel/Button_BuyMorePacks").GetComponent<Button>();
            _packOpenAnimationWrapperTransform = _wrapperTransform.Find("OpenAnimationWrapper").GetComponent<RectTransform>();
            Transform packOpener = _selfPage.transform.Find("PackOpenerWrapper/PackOpener/PackOpenerPanel");
            _packObjectsRoot = packOpener.Find("Packs/Offset/PacksRoot").gameObject;
            _packObjectsRootRectTransform = _packObjectsRoot.GetComponent<RectTransform>();
            _packObjectsRootHorizontalLayoutGroup = _packObjectsRoot.GetComponent<HorizontalLayoutGroup>();
            _currentPackTypeText = packOpener.Find("CurrentPackTypeText").GetComponent<TextMeshProUGUI>();
            _currentPackTypeAmountText = packOpener.Find("CurrentPackTypeAmountText").GetComponent<TextMeshProUGUI>();
            _openButton = packOpener.Find("Button_OpenPacks").GetComponent<Button>();
            _openButtonEventNotifier = _openButton.GetComponent<UIBehaviourEventNotifier>();
            _scrollLeftButton = packOpener.Find("Button_ArrowLeft").GetComponent<Button>();
            _scrollRightButton = packOpener.Find("Button_ArrowRight").GetComponent<Button>();
            _gooFillingAnimator = _wrapperTransform.Find("PackOpenerGooFilling").GetComponent<Animator>();

            _openedPackPanel.SetActive(false);

            _currentPackTypeText.text = "";
            _currentPackTypeAmountText.text = "";
            _buyMorePacksButton.onClick.AddListener(BuyMorePacksButtonHandler);
            _openButton.onClick.AddListener(OpenButtonHandler);
            _openButtonEventNotifier.OnPointerDownInvoked += OpenButtonPointerDownHandler;
            _openButtonEventNotifier.OnPointerUpInvoked += OpenButtonPointerUpHandler;
            _openedPackPanelCloseButton.onClick.AddListener(OpenedPackPanelCloseButtonHandler);
            _openedPackPanelOpenNextPackButton.onClick.AddListener(OpenedPackPanelOpenNextPackHandler);
            _scrollLeftButton.onClick.AddListener(() => ScrollButtonHandler(false));
            _scrollRightButton.onClick.AddListener(() => ScrollButtonHandler(true));

            _uiManager.DrawPopup<SideMenuPopup>(SideMenuPopup.MENU.MY_PACKS);
            _uiManager.DrawPopup<AreaBarPopup>();

            UpdateOpenButtonState();

            OneOf<Success, Exception> result = await _controller.Start();
            if (result.IsT1)
            {
                Log.Warn("Failed to start pack opener: " + result.Value);
                FailAndGoToMainMenu();
                return;
            }

            CreatePackObjects();
        }

        public void Hide()
        {
            Dispose();

            _uiManager.HidePopup<SideMenuPopup>();
            _uiManager.HidePopup<AreaBarPopup>();
        }

        public void Update()
        {
            if (_selfPage == null || !_selfPage.activeInHierarchy)
                return;

            _cardInfoPopupHandler.Update();
            float gooFillDelta = Time.deltaTime;
            gooFillDelta = _isOpenButtonBeingHeld ? gooFillDelta : -gooFillDelta * GooFillDecreaseMultiplier;
            _openButtonHoldTimeCounter = Mathf.Clamp(_openButtonHoldTimeCounter + gooFillDelta, 0f, GooFillMaxValue);
            _gooFillingAnimator.PlayInFixedTime(0, -1, _openButtonHoldTimeCounter);
        }

        public void Dispose()
        {
            ClearPackObjects();
            ClearCardOpenAnimation();

            Object.Destroy(_openedPackPanel);
            _controller?.Dispose();

            if (_selfPage == null)
                return;

            Object.Destroy(_selfPage);
            _selfPage = null;
        }

        #endregion

        private void PlayCardsHideAnimation(Action onEnd)
        {
            _openingPackAnimationObject.GetComponent<Animator>().enabled = false;
            for (int i = 0; i < _openedCards.Count; i++)
            {
                OpenedPackCard openedPackCard = _openedCards[i];
                Sequence hideSequence = DOTween.Sequence();
                hideSequence.AppendInterval(i * 0.1f);
                hideSequence.Append(openedPackCard.GameObject.transform.DOMove(CardHidePosition, 0.2f).SetEase(Ease.OutQuad));
                if (i == _openedCards.Count - 1)
                {
                    hideSequence.AppendInterval(1f);
                    hideSequence.AppendCallback(() => onEnd());
                }
            }
        }

        private void SetSelectedPackType(Enumerators.MarketplaceCardPackType? packType)
        {
            _selectedPackType = packType;
            _dataManager.CachedUserLocalData.PackOpenerLastSelectedPackType = packType;
            if (packType != null)
            {
                uint amount = _controller.GetPackTypeAmount(packType.Value);
                _currentPackTypeText.text = $"{packType.ToString().ToUpperInvariant()} PACK";
                _currentPackTypeAmountText.text = amount.ToString();

#if UNITY_EDITOR
                foreach (PackObject packObject in _packObjects)
                {
                    const string selectedString = "✔ ";
                    packObject.GameObject.name = packObject.GameObject.name.Replace(selectedString, "");
                    if (packObject.PackType == packType)
                    {
                        packObject.GameObject.name = selectedString + packObject.GameObject.name;
                    }
                }
#endif
            }
            else
            {
                _currentPackTypeText.text = "";
                _currentPackTypeAmountText.text = "";
            }

            UpdateOpenButtonState();
        }

        private async Task OpenSelectedPack(bool openingNextPack)
        {
            Log.Debug(nameof(OpenSelectedPack));
            Assert.IsTrue(_selectedPackType != null);
            OneOf<IReadOnlyList<CardKey>, Exception> result = await _controller.OpenPack(_selectedPackType.Value);
            if (result.IsT1)
            {
                ExceptionReporter.LogExceptionAsWarning(Log, result.AsT1);
                FailAndGoToMainMenu("Loading cards failed.\n Please try again.");
                return;
            }

            // Choose animation
            int packOpenAnimationIndex = -1;
            PackOpeningAnimationData packOpenAnimationData = null;
            if (openingNextPack)
            {
                packOpenAnimationIndex = _lastPackOpenAnimationIndex;
                packOpenAnimationData = _packOpenAnimationsData[_lastPackOpenAnimationIndex];
            }
            else
            {
                for (int i = _packOpenAnimationsData.Length - 1; i >= 0; i--)
                {
                    if (_openButtonHoldTimeCounter > _packOpenAnimationsData[i].OpenButtonHoldTriggerDuration)
                    {
                        packOpenAnimationIndex = i;
                        _lastPackOpenAnimationIndex = packOpenAnimationIndex;
                        packOpenAnimationData = _packOpenAnimationsData[packOpenAnimationIndex];
                        break;
                    }
                }
            }

            if (packOpenAnimationData == null)
                throw new ArgumentNullException(nameof(packOpenAnimationData));

            Log.Debug($"Goo fill value: {_openButtonHoldTimeCounter}, using pack opening animation {packOpenAnimationIndex}");

            // Setup UI blocker
            _openedPackPanel.SetActive(true);
            _openedPackPanelCloseButton.gameObject.SetActive(false);
            _openedPackPanelOpenNextPackButton.gameObject.SetActive(false);

            // Dim sequence had to be done in code to correctly work as UI blocker
            if (!openingNextPack)
            {
                _openedPackPanelCanvasGroup.alpha = 0;
            }

            Sequence dimSequence = DOTween.Sequence();
            dimSequence.AppendInterval(packOpenAnimationData.PreDimDelay);
            dimSequence.Append(_openedPackPanelCanvasGroup.DOFade(PreDimAlpha, packOpenAnimationData.PreDimDuration));
            dimSequence.AppendInterval(packOpenAnimationData.DimDelay - packOpenAnimationData.PreDimDelay - packOpenAnimationData.PreDimDuration);
            dimSequence.Append(_openedPackPanelCanvasGroup.DOFade(DimAlpha, packOpenAnimationData.DimDuration));

            // Update counter
            SetSelectedPackType(_selectedPackType.Value);

            // Start the animation
            _openingPackAnimationObject = Object.Instantiate(_packOpenAnimationPrefabs[packOpenAnimationIndex]);
            RectTransform animationRectTransform = _openingPackAnimationObject.GetComponent<RectTransform>();
            animationRectTransform.SetParent(_packOpenAnimationWrapperTransform);
            animationRectTransform.anchoredPosition = Vector3.zero;
            animationRectTransform.localScale = Vector3.one;
            PackOpeningAnimationEventsReceiver packOpeningAnimationEventsReceiver =
                _openingPackAnimationObject.GetComponent<PackOpeningAnimationEventsReceiver>();
            PackOpeningAnimationCardObjects packOpeningAnimationCardObjects =
                _openingPackAnimationObject.GetComponentInChildren<PackOpeningAnimationCardObjects>();

            // Cards flying out animation is relative to the center to the machine.
            // But since machine is off-center, cards open up off-center too, which looks weird.
            // So to mitigate this, as soon as cards start flying, we start moving them to the center of the screen.
            Vector3 screenCenterWordPoint =
                Camera.main.ScreenToWorldPoint(new Vector3(Screen.width / 2f, Screen.height / 2f, Camera.main.nearClipPlane));
            Vector3 openedCardsTargetPosition =
                screenCenterWordPoint - (packOpeningAnimationCardObjects.CardsRootTransformCenter.position - packOpeningAnimationCardObjects.CardsRootTransform.position);
            openedCardsTargetPosition.z = packOpeningAnimationCardObjects.CardsRootTransform.position.z;

            bool cardFlyOutAnimationStarted = false;
            void CardsFlyingStartedHandler()
            {
                cardFlyOutAnimationStarted = true;
                packOpeningAnimationEventsReceiver.CardsFlyingStarted -= CardsFlyingStartedHandler;

                packOpeningAnimationCardObjects.CardsRootTransform.DOMove(
                    openedCardsTargetPosition,
                    CardsFlyOutAnimationLength
                );
            }

            packOpeningAnimationEventsReceiver.CardsFlyingStarted += CardsFlyingStartedHandler;

            await new WaitUntil(() => cardFlyOutAnimationStarted);

            // Enable buttons
            _openedPackPanelCloseButton.interactable = true;
            _openedPackPanelOpenNextPackButton.interactable = true;

            _openedPackPanelCloseButton.gameObject.SetActive(false);
            _openedPackPanelOpenNextPackButton.gameObject.SetActive(false);

            // Fill missing cards with fakes
            IReadOnlyList<CardKey> cardKeys = result.AsT0;
            List<Card> cards = _dataManager.CachedCardsLibraryData.GetCardsByCardKeys(cardKeys, true).ToList();
            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                if (card == null)
                {
                    card = CreateEmptyPodFakeCard(cardKeys[i]);
                    cards[i] = card;
                }
            }

            ActivateOpenedCards(cards, packOpeningAnimationCardObjects.CardsObjects);
        }

        private void ActivateOpenedCards(List<Card> cards, GameObject[] cardObjects)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                OpenedPackCard openedPackCard = new OpenedPackCard(
                    cardObjects[i],
                    _cardsController,
                    card);

                openedPackCard.Button.onClick.AddListener(() =>
                {
                    if (!openedPackCard.IsFlipped)
                    {
                        openedPackCard.Flip();

                        bool allFlipped = _openedCards.TrueForAll(c => c.IsFlipped);
                        if (allFlipped)
                        {
                            _openedPackPanelCloseButton.gameObject.SetActive(true);
                            _openedPackPanelOpenNextPackButton.gameObject.SetActive(_controller.GetPackTypeAmount(_selectedPackType.Value) > 0);
                            _openedPackPanelBottomButtonsCanvasGroup
                                .DOFade(1f, 0.3f)
                                .ChangeStartValue(0)
                                .SetDelay(0.5f);

                            _controller.OnPackCollected();
                        }
                    }
                    else
                    {
                        if (_cardInfoPopupHandler.IsStateChanging)
                            return;

                        _cardInfoPopupHandler.SelectCard(openedPackCard.BoardCardView);

                        // Nicely hide the clicked card to avoid duplicating it visually
                        openedPackCard.GameObject.SetActive(false);

                        void OnClosed()
                        {
                            openedPackCard.GameObject.SetActive(true);
                            _cardInfoPopupHandler.Closed -= OnClosed;
                        }

                        _cardInfoPopupHandler.Closed += OnClosed;
                    }
                });

                _openedCards.Add(openedPackCard);
            }
        }

        private void CreatePackObjects()
        {
            IReadOnlyList<Enumerators.MarketplaceCardPackType> shownPackTypes =
                _controller.ShownPackTypes
                    .Where(packType => _controller.GetPackTypeAmount(packType) != 0)
                    .ToList();

            ClearPackObjects();

            int fakePackObjectsCount;
            switch (shownPackTypes.Count)
            {
                case 0:
                    fakePackObjectsCount = 0;
                    break;
                case 1:
                    fakePackObjectsCount = 1;
                    break;
                default:
                    fakePackObjectsCount = 2;
                    break;
            }

            for (int i = fakePackObjectsCount - 1; i >= 0; i--)
            {
                _fakeLeftPackObjects.Add(
                    new PackObject(
                        _packObjectsRoot.transform,
                        _loadObjectsManager,
                        shownPackTypes[shownPackTypes.Count - 1 - i]
                    )
                );
            }

            foreach (Enumerators.MarketplaceCardPackType packType in shownPackTypes)
            {
                PackObject packObject = new PackObject(_packObjectsRoot.transform, _loadObjectsManager, packType);
                _packObjects.Add(packObject);
            }

            for (int i = 0; i < fakePackObjectsCount; i++)
            {
                _fakeRightPackObjects.Add(
                    new PackObject(
                        _packObjectsRoot.transform,
                        _loadObjectsManager,
                        shownPackTypes[i]
                    )
                );
            }

#if UNITY_EDITOR
            _fakeLeftPackObjects.ForEach(pack => pack.GameObject.name = "=== " + pack.GameObject.name + " Fake");
            _fakeRightPackObjects.ForEach(pack => pack.GameObject.name = "=== " + pack.GameObject.name + " Fake");
#endif

            // If there is only 1 available pack type, hide the fake left/right packs
            if (shownPackTypes.Count <= 1)
            {
                _fakeLeftPackObjects.ForEach(pack => pack.SetIsVisible(false));
                _fakeRightPackObjects.ForEach(pack => pack.SetIsVisible(false));
            }
            else
            {
                _fakeLeftPackObjects.ForEach(pack => pack.SetIsVisible(true));
                _fakeRightPackObjects.ForEach(pack => pack.SetIsVisible(true));
            }

            _scrollLeftButton.interactable = _scrollRightButton.interactable = shownPackTypes.Count > 1;

            // Try to use the last selected pack type, otherwise just first type, if any
            Enumerators.MarketplaceCardPackType? selectedPackType = null;
            if (_dataManager.CachedUserLocalData.PackOpenerLastSelectedPackType != null)
            {
                if (shownPackTypes.Contains(_dataManager.CachedUserLocalData.PackOpenerLastSelectedPackType.Value))
                {
                    selectedPackType = _dataManager.CachedUserLocalData.PackOpenerLastSelectedPackType;
                }
            }

            if (selectedPackType == null)
            {
                if (_packObjects.Count > 0)
                {
                    selectedPackType = _packObjects[0].PackType;
                }
            }

            if (selectedPackType != null)
            {
                // Get index of pack object with matching pack type
                for (int i = 0; i < _packObjects.Count; i++)
                {
                    if (_packObjects[i].PackType == selectedPackType)
                    {
                        // Update initial scroll position to selected pack type
                        _packObjectsRootRectTransform.anchoredPosition = CalculatePackObjectsRootPositionByIndex(i);
                        break;
                    }
                }
            }

            SetSelectedPackType(selectedPackType);
        }

        private void UpdateOpenButtonState()
        {
            _openButton.interactable = _selectedPackType != null && _controller.GetPackTypeAmount(_selectedPackType.Value) > 0;
            _openButton.GetComponent<Image>().material = _openButton.interactable ? null : _grayScaleMaterial;
        }

        private void FailAndGoToMainMenu(string customMessage = null)
        {
            _uiManager.HidePopup<LoadingOverlayPopup>();
            _uiManager.DrawPopup<WarningPopup>(customMessage ?? "Something went wrong.\n Please try again.");
            GameClient.Get<IAppStateManager>().ChangeAppState(Enumerators.AppState.MAIN_MENU);
        }

        private void ClearCardOpenAnimation()
        {
            foreach (OpenedPackCard packCard in _openedCards)
            {
                packCard.Dispose();
            }

            _openedCards.Clear();

            Log.Debug("_openingPackAnimationObject == null: " + (_openingPackAnimationObject == null));
            if (_openingPackAnimationObject != null)
            {
                Object.Destroy(_openingPackAnimationObject);
                _openingPackAnimationObject = null;
            }
        }

        private void ClearPackObjects()
        {
            _packObjects.ForEach(pack => pack.Dispose());
            _packObjects.Clear();

            _fakeLeftPackObjects.ForEach(pack => pack.Dispose());
            _fakeLeftPackObjects.Clear();

            _fakeRightPackObjects.ForEach(pack => pack.Dispose());
            _fakeRightPackObjects.Clear();
        }

        private float CalculatePackShiftPerItem()
        {
            return PackWidth + _packObjectsRootHorizontalLayoutGroup.spacing;
        }

        private Vector2 CalculatePackObjectsRootPositionByIndex(int index)
        {
            index += _fakeLeftPackObjects.Count - 1;
            return new Vector2(index * -CalculatePackShiftPerItem(), 0f);
        }

        #region UI Handlers

        private void OpenedPackPanelOpenNextPackHandler()
        {
            if (_tutorialManager.BlockAndReport(_openedPackPanelOpenNextPackButton.name))
                return;

            _openedPackPanelCloseButton.interactable = false;
            _openedPackPanelOpenNextPackButton.interactable = false;
            _openedPackPanelBottomButtonsCanvasGroup.DOFade(0, FadeDuration);

            PlayCardsHideAnimation(async () =>
            {
                ClearCardOpenAnimation();
                await OpenSelectedPack(true);
            });
        }

        private async void OpenedPackPanelCloseButtonHandler()
        {
            if (_tutorialManager.BlockAndReport(_openedPackPanelCloseButton.name))
                return;

            _tutorialManager.ReportActivityAction(Enumerators.TutorialActivityAction.CardOpenerClosedOpenCardsScreen);

            _openedPackPanelCloseButton.interactable = false;
            _openedPackPanelOpenNextPackButton.interactable = false;
            foreach (OpenedPackCard packCard in _openedCards)
            {
                packCard.Button.interactable = false;
            }

            PlayCardsHideAnimation(() => {});

            await _controller.OnOpenedPackScreenClosed();

            Sequence sequence = DOTween.Sequence();
            sequence.AppendInterval(0.5f);
            sequence.Append(_openedPackPanelCanvasGroup.DOFade(0f, FadeDuration));
            sequence.AppendCallback(() =>
            {
                ClearCardOpenAnimation();
                _openedPackPanel.SetActive(false);
                CreatePackObjects();
            });
        }

        private async void OpenButtonHandler()
        {
            if (_tutorialManager.BlockAndReport(_openButton.name))
                return;

            PlayClickSound();
            await OpenSelectedPack(false);
        }

        private void ScrollButtonHandler(bool isRight)
        {
            if (_packObjects.Count == 0)
                return;

            PlayClickSound();

            int currentIndex = _packObjects.IndexOf(_packObjects.First(pack => pack.PackType == _selectedPackType));
            int newIndex = currentIndex + (isRight ? 1 : -1);
            int newIndexClamped = MathUtility.Repeat(newIndex, _packObjects.Count);

            /*Log.Debug(
                $"Current: [PackType: {_packObjects[currentIndex].PackType}, Index: {currentIndex}], " +
                $"New: [PackType: {_packObjects[newIndexClamped].PackType}, IndexClamped: {newIndexClamped}, Index: {newIndex}]");*/

            Vector2 newPosition = CalculatePackObjectsRootPositionByIndex(newIndex);
            Vector2 newPositionClamped = CalculatePackObjectsRootPositionByIndex(newIndexClamped);

            // To avoid ugly jumps when clicking VERY quickly while keeping it smooth,
            // only complete the previous sequence if delta is big
            float packSizesDelta =
                Mathf.Abs(_packObjectsRootRectTransform.anchoredPosition.x - newPosition.x) /
                CalculatePackShiftPerItem();
            if (packSizesDelta < 1f || packSizesDelta > 2.5f)
            {
                _currentPackScrollSequence?.Complete(true);
            }
            else
            {
                _currentPackScrollSequence?.Kill();
            }

            _currentPackScrollSequence = DOTween.Sequence();
            _currentPackScrollSequence.Append(_packObjectsRootRectTransform.DOAnchorPos(newPosition, PackScrollAnimationDuration));
            _currentPackScrollSequence.AppendCallback(() => _packObjectsRootRectTransform.anchoredPosition = newPositionClamped);

            SetSelectedPackType(_packObjects[newIndexClamped].PackType);
        }

        private void OpenButtonPointerDownHandler(PointerEventData arg0)
        {
            _isOpenButtonBeingHeld = true;
            _openButtonHoldTimeCounter = 0;
        }

        private void OpenButtonPointerUpHandler(PointerEventData arg0)
        {
            _isOpenButtonBeingHeld = false;
        }

        private void BuyMorePacksButtonHandler()
        {
            PlayClickSound();
            GameClient.Get<IAppStateManager>().ChangeAppState(Enumerators.AppState.SHOP);
        }

        #endregion

        #region Util

        private void PlayClickSound()
        {
            GameClient.Get<ISoundManager>().PlaySound(Enumerators.SoundType.CLICK, Constants.SfxSoundVolume, false, false, true);
        }

        private void OpenAlertDialog(string msg)
        {
            GameClient.Get<ISoundManager>()
                .PlaySound(Enumerators.SoundType.CHANGE_SCREEN,
                    Constants.SfxSoundVolume,
                    false,
                    false,
                    true);
            _uiManager.DrawPopup<WarningPopup>(msg);
        }

        private static Card CreateEmptyPodFakeCard(CardKey cardKey)
        {
            return new Card(
                cardKey,
                $"Card #{cardKey.MouldId.Id}",
                0,
                "",
                "",
                "embryo_pod",
                0,
                0,
                Enumerators.Faction.AIR,
                "",
                Enumerators.CardKind.CREATURE,
                Enumerators.CardRank.MINION,
                Enumerators.CardType.UNDEFINED,
                new List<AbilityData>(),
                new PictureTransform(new FloatVector3(0), new FloatVector3(0.4f)),
                Enumerators.UniqueAnimation.None,
                true
            );
        }

        #endregion

        private class PackObject
        {
            private Graphic[] _graphics;

            public Enumerators.MarketplaceCardPackType PackType { get; }

            public GameObject GameObject { get; }

            public PackObject(Transform parent, ILoadObjectsManager loadObjectsManager, Enumerators.MarketplaceCardPackType packType)
            {
                PackType = packType;
                GameObject =
                    Object.Instantiate(
                        loadObjectsManager.GetObjectByPath<GameObject>("Prefabs/UI/Elements/OpenPack/PackOpenerPack"),
                        parent);
                GameObject.name = $"PackOpenerPack [{packType}]";
                _graphics = GameObject.GetComponentsInChildren<Graphic>();
            }

            public void SetIsVisible(bool visible)
            {
                foreach (Graphic graphic in _graphics)
                {
                    graphic.enabled = visible;
                }
            }

            public void Dispose()
            {
                Object.Destroy(GameObject);
            }
        }

        private class OpenedPackCard
        {
            private GameObject _glowPlaneObject;

            public GameObject GameObject { get; }

            public GameObject CardBackGameObject { get; }

            public BoardCardView BoardCardView { get; }

            public Button Button { get; }

            public bool IsFlipped { get; private set; }

            public OpenedPackCard(GameObject gameObject, CardsController cardsController, IReadOnlyCard card)
            {
                GameObject = gameObject;
                _glowPlaneObject = GameObject.transform.Find("GlowPlane").gameObject;
                CardBackGameObject = GameObject.transform.Find("CardBack").gameObject;
                Button = GameObject.transform.Find("Button").GetComponent<Button>();

                CardModel cardModel = new CardModel(new WorkingCard(card, card, null));
                BoardCardView = cardsController.CreateBoardCardViewByModel(cardModel);
                BoardCardView.Transform.SetParent(GameObject.transform);

                BoardCardView.Transform.localScale = Vector3.one * BoardCardViewOpenedCardScale;
                BoardCardView.Transform.localRotation = Quaternion.identity;
                BoardCardView.Transform.localPosition = Vector3.zero;
                SortingGroup sortingGroup = BoardCardView.GameObject.GetComponent<SortingGroup>();
                sortingGroup.sortingLayerID = SRSortingLayers.GameUI1;
                sortingGroup.sortingOrder = 15;
                BoardCardView.SetHighlightingEnabled(false);
                BoardCardView.GameObject.SetActive(false);
            }

            public void Flip()
            {
                if (IsFlipped)
                    return;

                IsFlipped = true;
                BoardCardView.Transform.eulerAngles = new Vector3(0, -90, 0);
                Sequence sequence = DOTween.Sequence();
                sequence
                    .Append(
                        CardBackGameObject.transform
                            .DOLocalRotate(new Vector3(90, 0, 0), CardFlipAnimationDuration / 2f)
                            .SetEase(Ease.InSine)
                    );
                sequence.Join(
                    _glowPlaneObject.transform
                        .DOLocalRotate(new Vector3(180, 0, 0), CardFlipAnimationDuration / 2f)
                        .SetEase(Ease.InSine)
                );
                sequence.AppendCallback(() =>
                {
                    CardBackGameObject.SetActive(false);
                    _glowPlaneObject.SetActive(false);
                    BoardCardView.GameObject.SetActive(true);
                });
                sequence
                    .Append(
                        BoardCardView.Transform
                            .DORotate(new Vector3(0, 0, 0), CardFlipAnimationDuration / 2f)
                            .SetEase(Ease.OutSine)
                    );
            }

            public void Dispose()
            {
                Object.Destroy(GameObject);
            }
        }

        private abstract class PackOpenerControllerBase
        {
            public abstract IReadOnlyList<Enumerators.MarketplaceCardPackType> ShownPackTypes { get; }

            public abstract uint GetPackTypeAmount(Enumerators.MarketplaceCardPackType packType);

            public abstract Task<OneOf<Success, Exception>> Start();

            public abstract Task<OneOf<IReadOnlyList<CardKey>, Exception>> OpenPack(Enumerators.MarketplaceCardPackType packType);

            public virtual void OnPackCollected()
            {
            }

            public virtual Task<OneOf<Success, Exception>> OnOpenedPackScreenClosed()
            {
                return Task.FromResult(OneOf<Success, Exception>.FromT0(new Success()));
            }

            public virtual void Dispose()
            {
            }
        }

        private class TutorialPackOpenerController : PackOpenerControllerBase
        {
            private readonly ITutorialManager _tutorialManager;

            private readonly Dictionary<Enumerators.MarketplaceCardPackType, uint> _packTypeToPackAmount =
                new Dictionary<Enumerators.MarketplaceCardPackType, uint>();

            public TutorialPackOpenerController()
            {
                _tutorialManager = GameClient.Get<ITutorialManager>();
            }

            public override IReadOnlyList<Enumerators.MarketplaceCardPackType> ShownPackTypes { get; } = new[]
            {
                Enumerators.MarketplaceCardPackType.Booster
            };

            public override uint GetPackTypeAmount(Enumerators.MarketplaceCardPackType packType)
            {
                _packTypeToPackAmount.TryGetValue(packType, out uint amount);
                return amount;
            }

            public override Task<OneOf<Success, Exception>> Start()
            {
                _packTypeToPackAmount[Enumerators.MarketplaceCardPackType.Booster] =
                    (uint) _tutorialManager.CurrentTutorial.TutorialContent.TutorialReward.CardPackCount;
                return Task.FromResult((OneOf<Success, Exception>) new Success());
            }

            public override Task<OneOf<IReadOnlyList<CardKey>, Exception>> OpenPack(Enumerators.MarketplaceCardPackType packType)
            {
                _tutorialManager.ReportActivityAction(Enumerators.TutorialActivityAction.CardPackOpened);
                IReadOnlyList<CardKey> cards =
                    _tutorialManager
                        .GetCardForCardPack(5)
                        .Select(card => card.CardKey)
                        .ToArray();
                _packTypeToPackAmount[packType]--;
                return Task.FromResult(OneOf<IReadOnlyList<CardKey>, Exception>.FromT0(cards));
            }

            public override void OnPackCollected()
            {
                _tutorialManager.ReportActivityAction(Enumerators.TutorialActivityAction.CardPackCollected);
            }
        }

        private class NormalPackOpenerController : PackOpenerControllerBase
        {
            private readonly IUIManager _uiManager;
            private readonly PlasmachainBackendFacade _plasmaChainBackendFacade;
            private readonly BackendDataControlMediator _backendDataControlMediator;
            private readonly BackendFacade _backendFacade;
            private readonly IDataManager _dataManager;
            private readonly BackendDataSyncService _backendDataSyncService;
            private readonly INetworkActionManager _networkActionManager;

            private readonly Dictionary<Enumerators.MarketplaceCardPackType, uint> _packTypeToPackAmount =
                new Dictionary<Enumerators.MarketplaceCardPackType, uint>();

            private bool _isOpenedPackScreenActive;

            public NormalPackOpenerController()
            {
                _uiManager = GameClient.Get<IUIManager>();
                _plasmaChainBackendFacade = GameClient.Get<PlasmachainBackendFacade>();
                _backendDataControlMediator = GameClient.Get<BackendDataControlMediator>();
                _backendFacade = GameClient.Get<BackendFacade>();
                _dataManager = GameClient.Get<IDataManager>();
                _networkActionManager = GameClient.Get<INetworkActionManager>();
                _backendDataSyncService = GameClient.Get<BackendDataSyncService>();
            }

            public override IReadOnlyList<Enumerators.MarketplaceCardPackType> ShownPackTypes { get; } = new[]
            {
                Enumerators.MarketplaceCardPackType.Booster,
                Enumerators.MarketplaceCardPackType.Super,
                Enumerators.MarketplaceCardPackType.Air,
                Enumerators.MarketplaceCardPackType.Earth,
                Enumerators.MarketplaceCardPackType.Fire,
                Enumerators.MarketplaceCardPackType.Life,
                Enumerators.MarketplaceCardPackType.Toxic,
                Enumerators.MarketplaceCardPackType.Water,
                Enumerators.MarketplaceCardPackType.Binance,
                Enumerators.MarketplaceCardPackType.Tron
            };

            public override uint GetPackTypeAmount(Enumerators.MarketplaceCardPackType packType)
            {
                _packTypeToPackAmount.TryGetValue(packType, out uint amount);
                return amount;
            }

            public override async Task<OneOf<Success, Exception>> Start()
            {
                try
                {
                    _uiManager.DrawPopup<LoadingOverlayPopup>("Checking your packs...");
                    await _networkActionManager.ExecuteNetworkTask(async () =>
                    {
                        using (DAppChainClient client = await _plasmaChainBackendFacade.GetConnectedClient())
                        {
                            // Claim unclaimed packs
                            Log.Debug("Call GetPendingMintingTransactionReceipts");
                            Protobuf.GetPendingMintingTransactionReceiptsResponse mintingTransactionReceipts =
                                await _backendFacade.GetPendingMintingTransactionReceipts(_backendDataControlMediator.UserDataModel.UserId);
                            if (mintingTransactionReceipts.ReceiptCollection.Receipts.Count > 0)
                            {
                                _uiManager.DrawPopup<LoadingOverlayPopup>("Claiming packs...");
                            }

                            foreach (Protobuf.MintingTransactionReceipt receiptProtobuf in mintingTransactionReceipts.ReceiptCollection.Receipts)
                            {
                                AuthFiatApiFacade.TransactionReceipt receipt = receiptProtobuf.FromProtobuf();
                                Log.Debug($"Claiming receipt with TxId {receipt.TxId}");
                                try
                                {
                                    await _plasmaChainBackendFacade.ClaimPacks(client, receipt);
                                    Log.Info($"Claimed receipt with TxId {receipt.TxId} successfully!!");
                                }
                                catch (TxCommitException e)
                                {
                                    // Already claimed?
                                    Log.Warn("Already claimed? " + e);
                                }

                                Log.Debug($"Confirming receipt with TxId {receipt.TxId}");
                                await _backendFacade.ConfirmPendingMintingTransactionReceipt(
                                    _backendDataControlMediator.UserDataModel.UserId,
                                    receipt.TxId);
                            }

                            if (mintingTransactionReceipts.ReceiptCollection.Receipts.Count > 0)
                            {
                                _uiManager.DrawPopup<LoadingOverlayPopup>("Loading your packs...");
                            }

                            foreach (Enumerators.MarketplaceCardPackType packType in ShownPackTypes)
                            {
                                await Task.Run(() => UpdatePackBalanceAmount(client, packType));
                            }
                        }
                    });
                }
                catch (Exception e)
                {
                    return e;
                }
                finally
                {
                    _uiManager.HidePopup<LoadingOverlayPopup>();
                }

                return new Success();
            }

            public override async Task<OneOf<IReadOnlyList<CardKey>, Exception>> OpenPack(Enumerators.MarketplaceCardPackType packType)
            {
                if (!_isOpenedPackScreenActive)
                {
                    _isOpenedPackScreenActive = true;
                    _backendDataSyncService.ResetPendingCardCollectionSyncFlags();
                }

                _uiManager.DrawPopup<LoadingOverlayPopup>("Opening your pack...");
                try
                {
                    using (DAppChainClient client = await _plasmaChainBackendFacade.GetConnectedClient())
                    {
                        IReadOnlyList<CardKey> cardKeys =
                            await _plasmaChainBackendFacade.CallOpenPack(client, packType);
                        _backendDataSyncService.SetCollectionDataDirtyFlag();
                        _packTypeToPackAmount[packType]--;
                        return OneOf<IReadOnlyList<CardKey>, Exception>.FromT0(cardKeys);
                    }
                }
                catch (Exception e)
                {
                    return e;
                }
                finally
                {
                    _uiManager.HidePopup<LoadingOverlayPopup>();
                }
            }

            public override async Task<OneOf<Success, Exception>> OnOpenedPackScreenClosed()
            {
                Log.Debug(nameof(OnOpenedPackScreenClosed));
                _isOpenedPackScreenActive = false;

                return await _backendDataSyncService.UpdateCardCollectionWithUi(true);
            }

            public override void Dispose()
            {
            }

            private async Task UpdatePackBalanceAmount(DAppChainClient client, Enumerators.MarketplaceCardPackType packType)
            {
                Log.Debug($"{nameof(UpdatePackBalanceAmount)}({nameof(packType)} = {packType})");
                try
                {
                    _packTypeToPackAmount[packType] = await _plasmaChainBackendFacade.GetPackTypeBalance(client, packType);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to get balance for pack type {packType}", e);
                }
            }
        }

        // Used for UI debugging
        private class FakePackOpenerController : PackOpenerControllerBase
        {
            public FakePackOpenerController()
            {
                switch (Random.Range(0, 3))
                {
                    case 0:
                        ShownPackTypes = new[]
                        {
                            Enumerators.MarketplaceCardPackType.Booster
                        };
                        break;
                    case 1:
                        ShownPackTypes = new[]
                        {
                            Enumerators.MarketplaceCardPackType.Booster,
                            Enumerators.MarketplaceCardPackType.Binance
                        };
                        break;
                    case 2:
                        ShownPackTypes = new[]
                        {
                            Enumerators.MarketplaceCardPackType.Booster,
                            Enumerators.MarketplaceCardPackType.Super,
                            Enumerators.MarketplaceCardPackType.Air,
                            Enumerators.MarketplaceCardPackType.Earth,
                            Enumerators.MarketplaceCardPackType.Fire,
                            Enumerators.MarketplaceCardPackType.Life,
                            Enumerators.MarketplaceCardPackType.Toxic,
                            Enumerators.MarketplaceCardPackType.Water,
                            Enumerators.MarketplaceCardPackType.Binance,
                            Enumerators.MarketplaceCardPackType.Tron
                        };
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public override IReadOnlyList<Enumerators.MarketplaceCardPackType> ShownPackTypes { get; }

            public override uint GetPackTypeAmount(Enumerators.MarketplaceCardPackType packType)
            {
                return (uint) packType + 2;
            }

            public override Task<OneOf<Success, Exception>> Start()
            {
                return Task.FromResult((OneOf<Success, Exception>) new Success());
            }

            public override Task<OneOf<IReadOnlyList<CardKey>, Exception>> OpenPack(Enumerators.MarketplaceCardPackType packType)
            {
                return Task.FromResult(OneOf<IReadOnlyList<CardKey>, Exception>.FromT0(
                    Enumerable
                        .Range(1, 5)
                        .Select(i => new CardKey(new MouldId(i), Enumerators.CardVariant.Standard))
                        .ToList())
                );
            }

            public override void OnPackCollected()
            {
            }
        }
    }
}
