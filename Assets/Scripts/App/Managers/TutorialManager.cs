using System;
using System.Collections.Generic;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using UnityEngine;
using Newtonsoft.Json;
using Object = UnityEngine.Object;
using DG.Tweening;
using Loom.ZombieBattleground.BackendCommunication;
using System.Globalization;
using Newtonsoft.Json.Converters;
using Loom.ZombieBattleground.Helpers;
using System.Linq;
using UnityEngine.UI;

namespace Loom.ZombieBattleground
{
    public class TutorialManager : IService, ITutorialManager
    {
        private const string TutorialDataPath = "Data/tutorial_data";

        private IUIManager _uiManager;

        private ISoundManager _soundManager;

        private ILoadObjectsManager _loadObjectsManager;

        private BackendFacade _backendFacade;

        private BackendDataControlMediator _backendDataControlMediator;

        private IDataManager _dataManager;

        private IGameplayManager _gameplayManager;

        private BattlegroundController _battlegroundController;

        private IAnalyticsManager _analyticsManager;

        private OverlordsTalkingController _overlordsChatController;

        private HandPointerController _handPointerController;

        private List<TutorialDescriptionTooltipItem> _tutorialDescriptionTooltipItems;

        private List<Enumerators.TutorialActivityAction> _activitiesDoneDuringThisTurn;

        private List<Sequence> _overlordSaysPopupSequences;

        private List<string> _buttonsWasDeactivatedPreviousStep;

        public bool IsTutorial { get; private set; }

        private List<TutorialData> _tutorials;
        private List<TutorialStep> _tutorialSteps;
        private int _currentTutorialStepIndex;

        public TutorialData CurrentTutorial { get; private set; }
        public TutorialStep CurrentTutorialStep { get; private set; }

        public AnalyticsTimer TutorialDuration { get; set; }

        public List<string> BlockedButtons { get; private set; }

        public bool BattleShouldBeWonBlocker;

        public bool PlayerWon { get; set; }

        public int TutorialsCount
        {
            get { return _tutorials.Count; }
        }

        public void Dispose()
        {
        }

        public void Init()
        {
            _uiManager = GameClient.Get<IUIManager>();
            _soundManager = GameClient.Get<ISoundManager>();
            _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();
            _dataManager = GameClient.Get<IDataManager>();
            _gameplayManager = GameClient.Get<IGameplayManager>();
            _analyticsManager = GameClient.Get<IAnalyticsManager>();
            _backendFacade = GameClient.Get<BackendFacade>();
            _backendDataControlMediator = GameClient.Get<BackendDataControlMediator>();

            _overlordsChatController = _gameplayManager.GetController<OverlordsTalkingController>();
            _handPointerController = _gameplayManager.GetController<HandPointerController>();

            _battlegroundController = _gameplayManager.GetController<BattlegroundController>();

            _overlordSaysPopupSequences = new List<Sequence>();

            var settings = new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture,
                Converters = {
                    new StringEnumConverter()
                },
                CheckAdditionalContent = true,
                MissingMemberHandling = MissingMemberHandling.Error,
                TypeNameHandling = TypeNameHandling.Auto,
                Error = (sender, args) =>
                {
                    Debug.LogException(args.ErrorContext.Error);
                }
            };

            _tutorials = JsonConvert.DeserializeObject<List<TutorialData>>(_loadObjectsManager
                        .GetObjectByPath<TextAsset>(TutorialDataPath).text, settings);

            TutorialDuration = new AnalyticsTimer();

            _tutorialDescriptionTooltipItems = new List<TutorialDescriptionTooltipItem>();
            _activitiesDoneDuringThisTurn = new List<Enumerators.TutorialActivityAction>();
            _buttonsWasDeactivatedPreviousStep = new List<string>();
            BlockedButtons = new List<string>();
        }

        public bool CheckNextTutorial()
        {
            SetupTutorialById(_dataManager.CachedUserLocalData.CurrentTutorialId);

            if (!CurrentTutorial.IsGameplayTutorial())
            {
                GameClient.Get<IAppStateManager>().ChangeAppState(Enumerators.AppState.MAIN_MENU);

                StartTutorial();

                return true;
            }

            return false;
        }

        public void Update()
        {
            if (!IsTutorial)
                return;

            for (int i = 0; i < _tutorialDescriptionTooltipItems.Count; i++)
            {
                _tutorialDescriptionTooltipItems[i]?.Update();
            }

            if (!CurrentTutorial.IsGameplayTutorial())
            {
                if (Input.GetMouseButtonDown(0))
                {
                    ReportActivityAction(Enumerators.TutorialActivityAction.TapOnScreen);
                }
            }
        }

        public bool IsButtonBlockedInTutorial(string name)
        {
            if (!IsTutorial && !BattleShouldBeWonBlocker)
                return false;

            return BlockedButtons.Contains(name);
        }

        public void SetupTutorialById(int id)
        {
            if (CheckAvailableTutorial())
            {
                CurrentTutorial = _tutorials.Find(tutor => tutor.Id == id);
                _currentTutorialStepIndex = 0;
                _tutorialSteps = CurrentTutorial.TutorialContent.TutorialSteps;
                CurrentTutorialStep = _tutorialSteps[_currentTutorialStepIndex];

                if (CurrentTutorial.IsGameplayTutorial() && !CurrentTutorial.TutorialContent.ToGameplayContent().SpecificBattlegroundInfo.DisabledInitialization)
                {
                    FillTutorialDeck();
                }

                ClearToolTips();
            }

            IsTutorial = false;
        }

        public bool CheckAvailableTutorial()
        {
            int id = _dataManager.CachedUserLocalData.CurrentTutorialId;

            TutorialData tutorial = _tutorials.Find((x) => !x.Ignore &&
                x.Id >= _dataManager.CachedUserLocalData.CurrentTutorialId);

            if (tutorial != null)
            {
                _dataManager.CachedUserLocalData.CurrentTutorialId = tutorial.Id;
                _dataManager.SaveCache(Enumerators.CacheDataType.USER_LOCAL_DATA);
                return true;
            }
            return false;
        }

        public void StartTutorial()
        {
            if (IsTutorial)
                return;

            IsTutorial = true;

            if (CurrentTutorial.IsGameplayTutorial())
            {
                if (!CurrentTutorial.TutorialContent.ToGameplayContent().SpecificBattlegroundInfo.DisabledInitialization)
                {
                    _battlegroundController.SetupBattlegroundAsSpecific(CurrentTutorial.TutorialContent.ToGameplayContent().SpecificBattlegroundInfo);
                }

                _battlegroundController.TurnStarted += TurnStartedHandler;

                _gameplayManager.GetController<InputController>().PlayerPointerEnteredEvent += PlayerSelectedEventHandler;
                _gameplayManager.GetController<InputController>().UnitPointerEnteredEvent += UnitSelectedEventHandler;
            }

            for (int i = 0; i < _tutorialSteps.Count; i++)
            {
                _tutorialSteps[i].IsDone = false;
            }
            BattleShouldBeWonBlocker = false;
            PlayerWon = false;

            ClearToolTips();
            EnableStepContent(CurrentTutorialStep);

            StartTutorialEvent(CurrentTutorial.Id);
        }

        private void StartTutorialEvent(int currentTutorialId)
        {
            switch (currentTutorialId)
            {
                // Basic
                case 0:
                    SetStartTutorialEvent(AnalyticsManager.EventStartedTutorialBasic);
                    break;

                // Abilities
                case 1:
                    SetStartTutorialEvent(AnalyticsManager.EventStartedTutorialAbilities);
                    break;

                // Rank
                case 2:
                    SetStartTutorialEvent(AnalyticsManager.EventStartedTutorialRanks);
                    break;

                // Overflow
                case 3:
                    SetStartTutorialEvent(AnalyticsManager.EventStartedTutorialOverflow);
                    break;

                // Deck
                case 4:
                    SetStartTutorialEvent(AnalyticsManager.EventStartedTutorialDeck);
                    break;

                // battle
                case 5:
                    SetStartTutorialEvent(AnalyticsManager.EventStartedTutorialBattle);
                    break;
            }
        }

        private void SetStartTutorialEvent(string eventName)
        {
            TutorialDuration.StartTimer();
            _analyticsManager.SetEvent(eventName);
        }

        private void PlayerSelectedEventHandler(Player player)
        {
            SetTooltipsByOwnerIfHas(player.IsLocalPlayer ? Enumerators.TutorialObjectOwner.PlayerOverlord : Enumerators.TutorialObjectOwner.EnemyOverlord);
        }

        private void UnitSelectedEventHandler(BoardUnitView unit)
        {
            SetTooltipsStateIfHas(unit.Model.TutorialObjectId, true);
        }

        private void UnitDeselectedEventHandler(BoardUnitView unit)
        {
            SetTooltipsStateIfHas(unit.Model.TutorialObjectId, false);
        }

        private void TurnStartedHandler()
        {
            _activitiesDoneDuringThisTurn.Clear();
        }

        public void StopTutorial()
        {
            if (!IsTutorial)
                return;

            if (CurrentTutorial.IsGameplayTutorial())
            {
                _battlegroundController.TurnStarted -= TurnStartedHandler;

                _gameplayManager.GetController<InputController>().PlayerPointerEnteredEvent -= PlayerSelectedEventHandler;
                _gameplayManager.GetController<InputController>().UnitPointerEnteredEvent -= UnitSelectedEventHandler;
            }

            _uiManager.HidePopup<TutorialAvatarPopup>();

            _soundManager.StopPlaying(Enumerators.SoundType.TUTORIAL);

            if (BattleShouldBeWonBlocker)
                return;

            ClearToolTips();

            _dataManager.CachedUserLocalData.CurrentTutorialId++;

            if (_dataManager.CachedUserLocalData.CurrentTutorialId >= _tutorials.Count)
            {
                _dataManager.CachedUserLocalData.CurrentTutorialId = 0;
                _gameplayManager.IsTutorial = false;
                _dataManager.CachedUserLocalData.Tutorial = false;
                _gameplayManager.IsSpecificGameplayBattleground = false;
            }

            if (!CheckAvailableTutorial())
            {
                _gameplayManager.IsTutorial = false;
                _dataManager.CachedUserLocalData.Tutorial = false;
                _gameplayManager.IsSpecificGameplayBattleground = false;
            }


            _buttonsWasDeactivatedPreviousStep.Clear();

            IsTutorial = false;
            BattleShouldBeWonBlocker = false;
            _dataManager.SaveCache(Enumerators.CacheDataType.USER_LOCAL_DATA);

            CompleteTutorialEvent(CurrentTutorial.Id);
        }

        private void CompleteTutorialEvent(int currentTutorialId)
        {
            switch (currentTutorialId)
            {
                // Basic
                case 0:
                    SetCompleteTutorialEvent(AnalyticsManager.EventCompletedTutorialBasic);
                    break;

                // Abilities
                case 1:
                    SetCompleteTutorialEvent(AnalyticsManager.EventCompletedTutorialAbilities);
                    break;

                // Rank
                case 2:
                    SetCompleteTutorialEvent(AnalyticsManager.EventCompletedTutorialRanks);
                    break;

                // Overflow
                case 3:
                    SetCompleteTutorialEvent(AnalyticsManager.EventCompletedTutorialOverflow);
                    break;

                // Deck
                case 4:
                    SetCompleteTutorialEvent(AnalyticsManager.EventCompletedTutorialDeck);
                    break;

                // battle
                case 5:
                    SetCompleteTutorialEvent(AnalyticsManager.EventCompletedTutorialBattle);
                    break;
            }
        }

        private void SetCompleteTutorialEvent(string eventName)
        {
            TutorialDuration.FinishTimer();
            Dictionary<string, object> eventParameters = new Dictionary<string, object>();
            eventParameters.Add(AnalyticsManager.PropertyTutorialTimeToComplete, TutorialDuration.GetTimeDiffrence());
            _analyticsManager.SetEvent(eventName, eventParameters);
        }

        public SpecificTurnInfo GetCurrentTurnInfo()
        {
            if (!IsTutorial)
                return null;

            return CurrentTutorial.TutorialContent.ToGameplayContent().SpecificTurnInfos.Find(x => x.TurnIndex == _battlegroundController.CurrentTurn);
        }

        public bool IsCompletedActivitiesForThisTurn()
        {
            if (!IsTutorial)
                return true;

            if (GetCurrentTurnInfo() != null)
            {
                foreach (Enumerators.TutorialActivityAction activityAction in GetCurrentTurnInfo().RequiredActivitiesToDoneDuringTurn)
                {
                    if (!_activitiesDoneDuringThisTurn.Contains(activityAction))
                        return false;
                }
            }

            return true;
        }

        public void ReportActivityAction(Enumerators.TutorialActivityAction action, int sender = 0)
        {
            if (!IsTutorial)
                return;

            if (action == Enumerators.TutorialActivityAction.TapOnScreen)
            {
                HideAllActiveDescriptionTooltip();
            }

            bool skip = false;
            foreach (TutorialData tutorial in _tutorials)
            {
                if (tutorial.TutorialContent.ActionActivityHandlers != null)
                {
                    foreach (ActionActivityHandler activityHandler in tutorial.TutorialContent.ActionActivityHandlers)
                    {
                        if (activityHandler.TutorialActivityAction == action && !activityHandler.HasSpecificConnection)
                        {
                            DoActionByActivity(activityHandler);
                            skip = true;
                            break;
                        }
                    }
                }

                if (skip)
                    break;
            }

            if (CurrentTutorial.IsGameplayTutorial())
            {
                if (_battlegroundController.CurrentTurn > 1)
                {
                    SpecificTurnInfo specificTurnInfo = GetCurrentTurnInfo();

                    if (specificTurnInfo != null)
                    {
                        if (specificTurnInfo.ActionActivityHandlers != null)
                        {
                            foreach (ActionActivityHandler activity in specificTurnInfo.ActionActivityHandlers)
                            {
                                if (!_activitiesDoneDuringThisTurn.Contains(activity.ConnectedTutorialActivityAction) &&
                                    activity.TutorialActivityAction == action)
                                {
                                    DoActionByActivity(activity);
                                    break;
                                }
                            }
                        }

                        if (specificTurnInfo.RequiredActivitiesToDoneDuringTurn != null)
                        {
                            if (specificTurnInfo.RequiredActivitiesToDoneDuringTurn.Contains(action) &&
                               action != CurrentTutorialStep.ActionToEndThisStep)
                            {

                                List<TutorialStep> steps = CurrentTutorial.TutorialContent.TutorialSteps.FindAll(x =>
                                                             x.ToGameplayStep().ConnectedTurnIndex == specificTurnInfo.TurnIndex);

                                foreach (TutorialStep step in steps)
                                {
                                    if (step.ActionToEndThisStep == action && !step.IsDone)
                                    {
                                        if (step.ToGameplayStep().TutorialObjectIdStepOwner == 0 || step.ToGameplayStep().TutorialObjectIdStepOwner == sender)
                                        {
                                            step.IsDone = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (CurrentTutorialStep.ConnectedActivities != null)
            {
                ActionActivityHandler handler;
                foreach (int id in CurrentTutorialStep.ConnectedActivities)
                {
                    handler = CurrentTutorial.TutorialContent.ActionActivityHandlers.Find(x => x.Id == id && x.HasSpecificConnection);

                    if (handler != null)
                    {
                        if (handler.TutorialActivityAction == action)
                        {
                            DoActionByActivity(handler);
                            break;
                        }
                    }
                }
            }

            CheckTooltips(action, sender);

            if (CurrentTutorial.IsGameplayTutorial())
            {
                _activitiesDoneDuringThisTurn.Add(action);
            }

            if (CurrentTutorialStep != null && action == CurrentTutorialStep.ActionToEndThisStep)
            {
                MoveToNextStep();
            }
        }

        private void CheckTooltips(Enumerators.TutorialActivityAction action, int sender = 0)
        {
            Enumerators.TutorialObjectOwner owner;
            switch (action)
            {
                case Enumerators.TutorialActivityAction.BattleframeSelected:
                    SetTooltipsStateIfHas(sender, true);
                    break;
                case Enumerators.TutorialActivityAction.EnemyOverlordSelected:
                case Enumerators.TutorialActivityAction.PlayerOverlordSelected:
                    {
                        owner = action == Enumerators.TutorialActivityAction.PlayerOverlordSelected ?
                            Enumerators.TutorialObjectOwner.PlayerOverlord :
                            Enumerators.TutorialObjectOwner.EnemyOverlord;
                        SetTooltipsByOwnerIfHas(owner);
                    }
                    break;
                case Enumerators.TutorialActivityAction.PlayerManaBarSelected:
                    if (action == Enumerators.TutorialActivityAction.PlayerManaBarSelected)
                    {
                        SetTooltipsByOwnerIfHas(Enumerators.TutorialObjectOwner.PlayerGooBottles);
                    }
                    break;
                case Enumerators.TutorialActivityAction.PlayerCardInHandSelected:
                    SetTooltipsByOwnerIfHas(Enumerators.TutorialObjectOwner.PlayerCardInHand);
                    break;
                case Enumerators.TutorialActivityAction.IncorrectButtonTapped:
                    SetTooltipsByOwnerIfHas(Enumerators.TutorialObjectOwner.IncorrectButton);
                    break;
                default:
                    break;
            }
        }

        private void SetIncorrectButtonTooltip()
        {
            List<TutorialDescriptionTooltipItem> tooltips = _tutorialDescriptionTooltipItems.FindAll(x => x.OwnerType == Enumerators.TutorialObjectOwner.IncorrectButton);
            foreach (TutorialDescriptionTooltipItem tooltip in tooltips)
            {

            }
        }

        private void MoveToNextStep()
        {
            if (CurrentTutorialStep != null)
            {
                _handPointerController.ResetAll();
                ClearOverlordSaysPopupSequences();
                CurrentTutorialStep.IsDone = true;
            }

            if (_currentTutorialStepIndex + 1 >= _tutorialSteps.Count)
            {
                ClearToolTips();

                if (!CurrentTutorial.IsGameplayTutorial())
                {
                    StopTutorial();
                }
            }
            else
            {
                CurrentTutorialStep = GetNextNotDoneStep();

                EnableStepContent(CurrentTutorialStep);
            }
        }

        private TutorialStep GetNextNotDoneStep()
        {
            for (int i = _currentTutorialStepIndex + 1; i < _tutorialSteps.Count; i++)
            {
                if (!_tutorialSteps[i].IsDone)
                {
                    _currentTutorialStepIndex = i;
                    return _tutorialSteps[i];
                }
            }
            return _tutorialSteps[_currentTutorialStepIndex];
        }

        private async void EnableStepContent(TutorialStep step)
        {
            HideAllActiveDescriptionTooltip();

            _handPointerController.ResetAll();

            if (step.HandPointers != null)
            {
                foreach (HandPointerInfo handPointer in step.HandPointers)
                {
                    DrawPointer(handPointer.TutorialHandPointerType,
                                handPointer.TutorialHandPointerOwner,
                                (Vector3)handPointer.StartPosition,
                                (Vector3)handPointer.EndPosition,
                                handPointer.AppearDelay,
                                handPointer.AppearOnce,
                                handPointer.TutorialObjectIdStepOwner,
                                handPointer.TargetTutorialObjectId,
                                handPointer.AdditionalObjectIdOwners,
                                handPointer.AdditionalObjectIdTargets,
                                handPointer.TutorialHandLayer);
                }
            }

            if (step.TutorialDescriptionTooltipsToActivate != null)
            {
                foreach (int tooltipId in step.TutorialDescriptionTooltipsToActivate)
                {
                    TutorialDescriptionTooltip tooltip = CurrentTutorial.TutorialContent.TutorialDescriptionTooltips.Find(x => x.Id == tooltipId);

                    DrawDescriptionTooltip(tooltip.Id,
                                           tooltip.Description,
                                           tooltip.TutorialTooltipAlign,
                                           tooltip.TutorialTooltipOwner,
                                           tooltip.TutorialTooltipOwnerId,
                                           (Vector3)tooltip.Position,
                                           tooltip.Resizable,
                                           tooltip.AppearDelay,
                                           tooltip.DynamicPosition);
                }
            }

            if (step.TutorialDescriptionTooltipsToDeactivate != null)
            {
                foreach (int tooltipId in step.TutorialDescriptionTooltipsToDeactivate)
                {
                    DeactivateDescriptionTooltip(tooltipId);
                }
            }

            if (step.TutorialAvatar != null)
            {
                DrawAvatar(step.TutorialAvatar.Description, step.TutorialAvatar.DescriptionTooltipCloseText, step.TutorialAvatar.Pose, step.TutorialAvatar.AboveUI);
            }

            if (!string.IsNullOrEmpty(step.SoundToPlay))
            {
                PlayTutorialSound(step.SoundToPlay, step.SoundToPlayBeginDelay);
            }

            switch (step)
            {
                case TutorialGameplayStep gameStep:
                    if (gameStep.OverlordSayTooltips != null)
                    {
                        foreach (OverlordSayTooltipInfo tooltip in gameStep.OverlordSayTooltips)
                        {
                            DrawOverlordSayPopup(tooltip.Description,
                                                tooltip.TutorialTooltipAlign,
                                                tooltip.TutorialTooltipOwner,
                                                tooltip.AppearDelay,
                                                true,
                                                tooltip.Duration);
                        }
                    }

                    if(gameStep.LaunchGameplayManually)
                    {
                        _battlegroundController.StartGameplayTurns();
                    }

                    if (gameStep.PlayerOverlordAbilityShouldBeUnlocked)
                    {
                        if (!CurrentTutorial.TutorialContent.ToGameplayContent().SpecificBattlegroundInfo.DisabledInitialization &&
                            CurrentTutorial.TutorialContent.ToGameplayContent().
                            SpecificBattlegroundInfo.PlayerInfo.PrimaryOverlordAbility != Enumerators.OverlordSkill.NONE)
                        {
                            _gameplayManager.GetController<SkillsController>().PlayerPrimarySkill.SetCoolDown(0);
                        }
                    }

                    if (gameStep.MatchShouldBePaused)
                    {
                        Time.timeScale = 0;
                    }
                    else
                    {
                        if (Time.timeScale == 0)
                        {
                            Time.timeScale = 1;
                        }
                    }

                    if (gameStep.LaunchAIBrain || (!gameStep.AIShouldBePaused && _gameplayManager.GetController<AIController>().AIPaused))
                    {
                        await _gameplayManager.GetController<AIController>().LaunchAIBrain();
                    }

                    if(_gameplayManager.GetController<AIController>().IsBrainWorking)
                    {
                        await _gameplayManager.GetController<AIController>().SetTutorialStep();
                    }

                    if (gameStep.ActionToEndThisStep == Enumerators.TutorialActivityAction.YouWonPopupOpened)
                    {
                        GameClient.Get<IGameplayManager>().EndGame(Enumerators.EndGameType.WIN, 0);
                    }

                    if (CurrentTutorial.TutorialContent.ToGameplayContent().GameplayFlowBeginsManually && gameStep.BeginGameplayFlowManually)
                    {
                        (_gameplayManager as GameplayManager).TutorialStartAction?.Invoke();
                    }

                    break;
                case TutorialMenuStep menuStep:

                    BlockedButtons.Clear();

                    if (!string.IsNullOrEmpty(menuStep.OpenScreen))
                    {
                        if (menuStep.OpenScreen.EndsWith("Popup"))
                        {
                            _uiManager.DrawPopupByName(menuStep.OpenScreen);
                        }
                        else if (menuStep.OpenScreen.EndsWith("Page"))
                        {
                            _uiManager.SetPageByName(menuStep.OpenScreen);
                        }
                    }

                    if (menuStep.BlockedButtons != null)
                    {
                        BlockedButtons.AddRange(menuStep.BlockedButtons);
                    }

                    if(menuStep.BattleShouldBeWonBlocker && !PlayerWon)
                    {
                        BattleShouldBeWonBlocker = true;
                    }

                    break;
            }
        }

        public void SetStatusOfButtonsByNames(List<string> buttons, bool status)
        {
            GameObject buttonObject;
            Button buttonComponent;

            foreach (string button in buttons)
            {
                buttonObject = GameObject.Find(button);

                if (buttonObject != null)
                {
                    buttonComponent = buttonObject.GetComponent<Button>();

                    if (buttonComponent != null && buttonComponent)
                    {
                        buttonComponent.interactable = status;

                        if (!status)
                        {
                            _buttonsWasDeactivatedPreviousStep.Add(button);
                        }
                    }
                    else
                    {
                        MenuButtonNoGlow menuButtonNoGlow = buttonObject.GetComponent<MenuButtonNoGlow>();
                        if (menuButtonNoGlow != null && menuButtonNoGlow)
                        {
                            menuButtonNoGlow.enabled = status;
                            if (!status)
                            {
                                _buttonsWasDeactivatedPreviousStep.Add(button);
                            }
                        }
                        else
                        {
                            buttonObject.SetActive(status);
                            if (!status)
                            {
                                _buttonsWasDeactivatedPreviousStep.Add(button);
                            }
                        }
                    }
                }
            }
        }

        public string GetCardNameById(int id)
        {
            SpecificBattlegroundInfo battleInfo = CurrentTutorial.TutorialContent.ToGameplayContent().SpecificBattlegroundInfo;

            List<SpecificBattlegroundInfo.OverlordCardInfo> cards = new List<SpecificBattlegroundInfo.OverlordCardInfo>();



            cards.AddRange(battleInfo.PlayerInfo.CardsInDeck);
            cards.AddRange(battleInfo.PlayerInfo.CardsInHand);
            cards.AddRange(battleInfo.PlayerInfo.CardsOnBoard.Select((info) => new SpecificBattlegroundInfo.OverlordCardInfo()
            {
                Name = info.Name,
                TutorialObjectId = info.TutorialObjectId
            })
            .ToList());
            cards.AddRange(battleInfo.OpponentInfo.CardsInDeck);
            cards.AddRange(battleInfo.OpponentInfo.CardsInHand);

            return cards.Find(x => x.TutorialObjectId == id)?.Name;
        }

        public void SetTooltipsStateIfHas(int ownerId, bool isActive)
        {
            if (ownerId == 0)
                return;

            TutorialStep step;

            List<TutorialDescriptionTooltip> tooltips = CurrentTutorial.TutorialContent.TutorialDescriptionTooltips.FindAll(tooltip => tooltip.TutorialTooltipOwnerId == ownerId &&
                (tooltip.TutorialTooltipOwner == Enumerators.TutorialObjectOwner.EnemyBattleframe ||
                tooltip.TutorialTooltipOwner == Enumerators.TutorialObjectOwner.PlayerBattleframe));
            foreach (TutorialDescriptionTooltip tooltip in tooltips)
            {
                step = CurrentTutorial.TutorialContent.TutorialSteps.Find(info => info.ToGameplayStep().TutorialDescriptionTooltipsToActivate.Exists(id => id == tooltip.Id));
                if (step != null && (step.IsDone || step == CurrentTutorialStep))
                {
                    ActivateDescriptionTooltip(tooltip.Id);
                }
            }
        }

        public void SetTooltipsByOwnerIfHas(Enumerators.TutorialObjectOwner owner)
        {
            if (_gameplayManager.GetController<BoardArrowController>().CurrentBoardArrow != null)
                return;

            List<TutorialDescriptionTooltipItem> tooltips = _tutorialDescriptionTooltipItems.FindAll(x => x.OwnerType == owner);

            if (tooltips.Count > 0)
            {
                foreach (TutorialDescriptionTooltipItem tooltip in tooltips)
                {
                    ActivateDescriptionTooltip(tooltip.Id);
                }
            }
        }

        public void SetupBattleground(SpecificBattlegroundInfo specificBattleground)
        {
            _battlegroundController.SetupBattlegroundAsSpecific(specificBattleground);
        }

        public void FillTutorialDeck()
        {
            _gameplayManager.CurrentPlayerDeck =
                         new Deck(0, CurrentTutorial.TutorialContent.ToGameplayContent().SpecificBattlegroundInfo.PlayerInfo.OverlordId,
                         "TutorialDeck", new List<DeckCardData>(),
                         CurrentTutorial.TutorialContent.ToGameplayContent().SpecificBattlegroundInfo.PlayerInfo.PrimaryOverlordAbility,
                         CurrentTutorial.TutorialContent.ToGameplayContent().SpecificBattlegroundInfo.PlayerInfo.SecondaryOverlordAbility);

            _gameplayManager.OpponentPlayerDeck =
                        new Deck(0, CurrentTutorial.TutorialContent.ToGameplayContent().SpecificBattlegroundInfo.OpponentInfo.OverlordId,
                        "TutorialDeckOpponent", new List<DeckCardData>(),
                        CurrentTutorial.TutorialContent.ToGameplayContent().SpecificBattlegroundInfo.OpponentInfo.PrimaryOverlordAbility,
                        CurrentTutorial.TutorialContent.ToGameplayContent().SpecificBattlegroundInfo.OpponentInfo.SecondaryOverlordAbility);
        }

        public void PlayTutorialSound(string sound, float delay = 0f)
        {
            InternalTools.DoActionDelayed(() =>
            {
                _soundManager.PlaySound(Enumerators.SoundType.TUTORIAL, 0, sound, Constants.TutorialSoundVolume, false);
            }, delay);
        }

        public void DrawAvatar(string description, string hideAvatarButtonText, Enumerators.TutorialAvatarPose pose, bool aboveUI)
        {
            _uiManager.DrawPopup<TutorialAvatarPopup>(new object[]
            {
                description,
                hideAvatarButtonText,
                pose,
                aboveUI
            });
        }

        public void DrawPointer(Enumerators.TutorialHandPointerType type,
                                Enumerators.TutorialObjectOwner owner,
                                Vector3 begin,
                                Vector3? end = null,
                                float appearDelay = 0,
                                bool appearOnce = false,
                                int tutorialObjectIdStepOwner = 0,
                                int targetTutorialObjectId = 0,
                                List<int> additionalObjectIdOwners = null,
                                List<int> additionalObjectIdTargets = null,
                                Enumerators.TutorialObjectLayer handLayer = Enumerators.TutorialObjectLayer.Default)
        {
            _handPointerController.DrawPointer(type,
                                               owner,
                                               begin,
                                               end,
                                               appearDelay,
                                               appearOnce,
                                               tutorialObjectIdStepOwner,
                                               targetTutorialObjectId,
                                               additionalObjectIdOwners,
                                               additionalObjectIdTargets,
                                               handLayer);
        }

        public void DrawDescriptionTooltip(int id,
                                           string description,
                                           Enumerators.TooltipAlign align,
                                           Enumerators.TutorialObjectOwner owner,
                                           int ownerId,
                                           Vector3 position,
                                           bool resizable,
                                           float appearDelay,
                                           bool dynamicPosition,
                                           Enumerators.TutorialObjectLayer layer = Enumerators.TutorialObjectLayer.Default)
        {
            if (appearDelay > 0)
            {
                InternalTools.DoActionDelayed(() =>
                {
                    TutorialDescriptionTooltipItem tooltipItem = new TutorialDescriptionTooltipItem(id,
                                                                                                    description,
                                                                                                    align,
                                                                                                    owner,
                                                                                                    ownerId,
                                                                                                    position,
                                                                                                    resizable,
                                                                                                    dynamicPosition,
                                                                                                    layer);

                    _tutorialDescriptionTooltipItems.Add(tooltipItem);
                }, appearDelay);
            }
            else
            {
                TutorialDescriptionTooltipItem tooltipItem = new TutorialDescriptionTooltipItem(id,
                                                                                                description,
                                                                                                align,
                                                                                                owner,
                                                                                                ownerId,
                                                                                                position,
                                                                                                resizable,
                                                                                                dynamicPosition,
                                                                                                layer);

                _tutorialDescriptionTooltipItems.Add(tooltipItem);
            }
        }

        public void ActivateDescriptionTooltip(int id)
        {
            TutorialDescriptionTooltipItem tooltip = _tutorialDescriptionTooltipItems.Find(x => x.Id == id);

            if (tooltip == null)
            {
                TutorialDescriptionTooltip tooltipInfo = CurrentTutorial.TutorialContent.TutorialDescriptionTooltips.Find(x => x.Id == id);

                DrawDescriptionTooltip(tooltipInfo.Id,
                                       tooltipInfo.Description,
                                       tooltipInfo.TutorialTooltipAlign,
                                       tooltipInfo.TutorialTooltipOwner,
                                       tooltipInfo.TutorialTooltipOwnerId,
                                       (Vector3)tooltipInfo.Position,
                                       tooltipInfo.Resizable,
                                       tooltipInfo.AppearDelay,
                                       tooltipInfo.DynamicPosition,
                                       tooltipInfo.TutorialTooltipLayer);
            }
            else
            {
                tooltip.Show();
            }
        }

        public void ActivateDescriptionTooltipByOwner(Enumerators.TutorialObjectOwner owner, Vector3 position)
        {
            TutorialDescriptionTooltipItem tooltip = _tutorialDescriptionTooltipItems.Find(x => x.OwnerType == owner);

            if (tooltip == null)
            {
                TutorialDescriptionTooltip tooltipInfo = CurrentTutorial.TutorialContent.TutorialDescriptionTooltips.Find(x => x.TutorialTooltipOwner == owner);

                if (tooltipInfo == null)
                    return;

                DrawDescriptionTooltip(tooltipInfo.Id,
                                       tooltipInfo.Description,
                                       tooltipInfo.TutorialTooltipAlign,
                                       tooltipInfo.TutorialTooltipOwner,
                                       tooltipInfo.TutorialTooltipOwnerId,
                                       position,
                                       tooltipInfo.Resizable,
                                       tooltipInfo.AppearDelay,
                                       tooltipInfo.DynamicPosition);
            }
            else
            {
                tooltip.Show(position);
            }
        }

        public TutorialDescriptionTooltipItem GetDescriptionTooltip(int id)
        {
            return _tutorialDescriptionTooltipItems.Find(x => x.Id == id);
        }

        public void HideDescriptionTooltip(int id)
        {
            _tutorialDescriptionTooltipItems.Find(x => x.Id == id)?.Hide();
        }

        public void HideAllActiveDescriptionTooltip()
        {
            foreach (TutorialDescriptionTooltipItem tooltip in _tutorialDescriptionTooltipItems)
            {
                tooltip?.Hide();
            }
        }

        public void DeactivateDescriptionTooltip(int id)
        {
            TutorialDescriptionTooltipItem tooltip = _tutorialDescriptionTooltipItems.Find(x => x.Id == id);

            if (tooltip != null)
            {
                tooltip.Dispose();
                _tutorialDescriptionTooltipItems.Remove(tooltip);
            }
        }

        private void ClearToolTips()
        {
            foreach (TutorialDescriptionTooltipItem tooltip in _tutorialDescriptionTooltipItems)
            {
                tooltip.Dispose();
            }
            _tutorialDescriptionTooltipItems.Clear();
        }

        public void DrawOverlordSayPopup(string description,
                                        Enumerators.TooltipAlign align,
                                        Enumerators.TutorialObjectOwner owner,
                                        float appearDelay,
                                        bool ofStep = false,
                                        float duration = Constants.OverlordTalkingPopupDuration)
        {
            Sequence sequence = InternalTools.DoActionDelayed(() =>
            {
                _overlordsChatController.DrawOverlordSayPopup(description, align, owner, duration);
            }, appearDelay);

            if (ofStep)
            {
                _overlordSaysPopupSequences.Add(sequence);
            }
        }

        private void ClearOverlordSaysPopupSequences()
        {
            foreach (Sequence sequence in _overlordSaysPopupSequences)
            {
                sequence?.Kill();
            }
            _overlordSaysPopupSequences.Clear();
        }

        public void ActivateSelectHandPointer(Enumerators.TutorialObjectOwner owner)
        {
            _handPointerController.ChangeVisibilitySelectHandPointer(owner, true);
        }

        public void DeactivateSelectHandPointer(Enumerators.TutorialObjectOwner owner)
        {
            _handPointerController.ChangeVisibilitySelectHandPointer(owner, false);
        }

        private void DoActionByActivity(ActionActivityHandler activity)
        {
            switch (activity.TutorialActivityActionHandler)
            {
                case Enumerators.TutorialActivityActionHandler.OverlordSayTooltip:
                    {
                        OverlordSayTooltipInfo data = activity.TutorialActivityActionHandlerData as OverlordSayTooltipInfo;
                        DrawOverlordSayPopup(data.Description, data.TutorialTooltipAlign, data.TutorialTooltipOwner, data.AppearDelay, duration: data.Duration);
                    }
                    break;
                case Enumerators.TutorialActivityActionHandler.DrawDescriptionTooltips:
                    {
                        DrawDescriptionTooltipsInfo data = activity.TutorialActivityActionHandlerData as DrawDescriptionTooltipsInfo;
                        foreach (int id in data.TutorialDescriptionTooltipsToActivate)
                        {
                            ActivateDescriptionTooltip(id);
                        }
                    }
                    break;
            }
        }
    }
}
