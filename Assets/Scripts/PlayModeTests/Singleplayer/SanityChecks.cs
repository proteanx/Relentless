using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace Loom.ZombieBattleground.Test
{
    public class SanityChecks : BaseIntegrationTest
    {
        [UnityTest]
        [Timeout(500000)]
        public async Task Test_S1_SkipTutorials()
        {
            TestHelper.SetTestName("SanityChecks - Tutorial Skip");

            await TestHelper.ClickGenericButton("Button_Play");

            await TestHelper.AssertIfWentDirectlyToTutorial(
                TestHelper.GoBackToMainAndPressPlay);

            #region Tutorial Skip

            await TestHelper.AssertCurrentPageName("PlaySelectionPage");
            await TestHelper.ClickGenericButton("Button_Tutorial");
            await TestHelper.AssertCurrentPageName("GameplayPage");
            await SkipTutorial(false);

            #endregion

            await TestHelper.LetsThink();

            TestHelper.TestEndHandler();
        }

        [UnityTest]
        [Timeout(500000)]
        public async Task Test_S2_PlayThroughTutorials()
        {
            TestHelper.SetTestName("SanityChecks - Tutorial Non-Skip");

            await TestHelper.ClickGenericButton("Button_Play");

            await TestHelper.AssertIfWentDirectlyToTutorial(
                TestHelper.GoBackToMainAndPressPlay);

            #region Tutorial Non-Skip

            await TestHelper.AssertCurrentPageName("PlaySelectionPage");
            await TestHelper.ClickGenericButton("Button_Tutorial");
            await TestHelper.AssertCurrentPageName("GameplayPage");

            await PlayTutorial_Part1();

            await TestHelper.ClickGenericButton("Button_Continue");

            await PlayTutorial_Part2();

            await TestHelper.ClickGenericButton("Button_Continue");
            await TestHelper.AssertCurrentPageName("HordeSelectionPage");

            #endregion

            await TestHelper.LetsThink();

            TestHelper.TestEndHandler();
        }

        [UnityTest]
        [Timeout(500000)]
        public async Task Test_S3_CreateAHorde()
        {
            TestHelper.SetTestName("SanityChecks - Create a Horde and save");

            await TestHelper.ClickGenericButton("Button_Play");

            await TestHelper.AssertIfWentDirectlyToTutorial(
                TestHelper.GoBackToMainAndPressPlay);

            await TestHelper.AssertCurrentPageName("PlaySelectionPage");
            await TestHelper.ClickGenericButton("Button_SoloMode");
            await TestHelper.AssertCurrentPageName("HordeSelectionPage");

            await TestHelper.SelectAHordeByName("Razu", false);
            if (TestHelper.SelectedHordeIndex != -1)
            {
                await TestHelper.RemoveAHorde(TestHelper.SelectedHordeIndex);
            }

            await TestHelper.AddRazuHorde();
            await TestHelper.AssertCurrentPageName("HordeSelectionPage");

            TestHelper.TestEndHandler();
        }

        [UnityTest]
        [Timeout(900000)]
        public async Task Test_S4_PlayWithNewHorde()
        {
            TestHelper.SetTestName("SanityChecks - Gameplay with Razu");

            #region Solo Gameplay

            await TestHelper.ClickGenericButton("Button_Play");

            await TestHelper.AssertIfWentDirectlyToTutorial(
                TestHelper.GoBackToMainAndPressPlay);

            await TestHelper.AssertCurrentPageName("PlaySelectionPage");
            await TestHelper.ClickGenericButton("Button_SoloMode");
            await TestHelper.AssertCurrentPageName("HordeSelectionPage");
            await TestHelper.SelectAHordeByName("Razu");
            TestHelper.RecordExpectedOverlordName(TestHelper.SelectedHordeIndex);
            await TestHelper.ClickGenericButton("Button_Battle");
            await TestHelper.AssertCurrentPageName("GameplayPage");
            await SoloGameplay(true);
            await TestHelper.ClickGenericButton("Button_Continue");
            await TestHelper.AssertCurrentPageName("HordeSelectionPage");

            #endregion

            TestHelper.TestEndHandler();
        }

        [UnityTest]
        [Timeout(900000)]
        public async Task Test_S5_PlayWithDefaultHorde()
        {
            TestHelper.SetTestName("SanityChecks - Gameplay with Default");

            #region Solo Gameplay

            await TestHelper.ClickGenericButton("Button_Play");

            await TestHelper.AssertIfWentDirectlyToTutorial(
                TestHelper.GoBackToMainAndPressPlay);

            await TestHelper.AssertCurrentPageName("PlaySelectionPage");
            await TestHelper.ClickGenericButton("Button_SoloMode");
            await TestHelper.AssertCurrentPageName("HordeSelectionPage");

            int selectedHordeIndex = 0;

            await TestHelper.SelectAHordeByIndex(selectedHordeIndex);
            TestHelper.RecordExpectedOverlordName(selectedHordeIndex);
            await TestHelper.ClickGenericButton("Button_Battle");
            await TestHelper.AssertCurrentPageName("GameplayPage");
            await SoloGameplay(true);
            await TestHelper.ClickGenericButton("Button_Continue");
            await TestHelper.AssertCurrentPageName("HordeSelectionPage");

            #endregion

            TestHelper.TestEndHandler();
        }

        private async Task SoloGameplay(bool assertOverlordName = false)
        {
            if (TestHelper.IsTestFailed)
            {
                return;
            }

            TestHelper.InitalizePlayer();

            if (!TestHelper.IsTestFailed)
                await TestHelper.WaitUntilPlayerOrderIsDecided();

            if (assertOverlordName)
            {
                TestHelper.AssertOverlordName();
            }

            if (!TestHelper.IsTestFailed)
                await TestHelper.AssertMulliganPopupCameUp(
                    TestHelper.DecideWhichCardsToPick,
                    null);

            if (!TestHelper.IsTestFailed)
                await TestHelper.WaitUntilOurFirstTurn();

            if (!TestHelper.IsTestFailed)
                await TestHelper.MakeMoves();

            await new WaitForUpdate();
        }

        private async Task SkipTutorial(bool twoSteps = true)
        {
            await new WaitForSeconds(8);
            await TestHelper.ClickGenericButton("Button_Skip");

            await TestHelper.RespondToYesNoOverlay(true);

            if (twoSteps)
            {
                await TestHelper.ClickGenericButton("Button_Skip");

                await TestHelper.RespondToYesNoOverlay(true);
            }

            await new WaitForUpdate();
        }

        private async Task PlayTutorial_Part1()
        {
            if (TestHelper.IsTestFailed)
            {
                return;
            }

            await TestHelper.ClickGenericButton("Button_Next", count: 3);
            await TestHelper.ClickGenericButton("Button_Play");
            await TestHelper.ClickGenericButton("Button_Next", count: 4);

            await TestHelper.WaitUntilWeHaveACardAtHand();
            await TestHelper.PlayCardFromHandToBoard(new[]
            {
                0
            });

            await TestHelper.ClickGenericButton("Button_Next");

            await TestHelper.EndTurn();

            await TestHelper.WaitUntilCardIsAddedToBoard("OpponentBoard");
            await TestHelper.WaitUntilAIBrainStops();

            await TestHelper.ClickGenericButton("Button_Next");

            await TestHelper.WaitUntilOurTurnStarts();
            await TestHelper.WaitUntilInputIsUnblocked();

            await TestHelper.LetsThink();
            await TestHelper.LetsThink();
            await TestHelper.LetsThink();

            await TestHelper.PlayCardFromBoardToOpponent(new[]
                {
                    0
                },
                new[]
                {
                    0
                });

            for (int i = 0; i < 2; i++)
            {
                await TestHelper.ClickGenericButton("Button_Next");
            }

            await TestHelper.EndTurn();

            await TestHelper.WaitUntilOurTurnStarts();
            await TestHelper.WaitUntilInputIsUnblocked();

            await TestHelper.ClickGenericButton("Button_Next");

            await TestHelper.LetsThink();
            await TestHelper.LetsThink();
            await TestHelper.LetsThink();

            await TestHelper.PlayCardFromBoardToOpponent(new[]
                {
                    0
                },
                null,
                true);

            await TestHelper.ClickGenericButton("Button_Next");

            await TestHelper.LetsThink();
            await TestHelper.LetsThink();

            await TestHelper.EndTurn();

            await TestHelper.WaitUntilOurTurnStarts();
            await TestHelper.WaitUntilInputIsUnblocked();

            await TestHelper.ClickGenericButton("Button_Next");

            await TestHelper.LetsThink();
            await TestHelper.LetsThink();
            await TestHelper.LetsThink();

            await TestHelper.PlayCardFromBoardToOpponent(new[]
                {
                    0
                },
                new[]
                {
                    0
                });

            for (int i = 0; i < 3; i++)
            {
                await TestHelper.ClickGenericButton("Button_Next");
            }

            await TestHelper.WaitUntilAIBrainStops();
            await TestHelper.WaitUntilInputIsUnblocked();

            await TestHelper.LetsThink();
            await TestHelper.LetsThink();
            await TestHelper.LetsThink();

            await TestHelper.PlayCardFromHandToBoard(new[]
            {
                1
            });

            await TestHelper.LetsThink();
            await TestHelper.LetsThink();
            await TestHelper.LetsThink();

            await TestHelper.PlayCardFromBoardToOpponent(new[]
                {
                    0
                },
                null,
                true);

            await TestHelper.LetsThink();
            await TestHelper.LetsThink();

            for (int i = 0; i < 3; i++)
            {
                await TestHelper.ClickGenericButton("Button_Next");
            }

            await TestHelper.UseSkillToOpponentPlayer();

            for (int i = 0; i < 4; i++)
            {
                await TestHelper.ClickGenericButton("Button_Next");
            }

            await new WaitForUpdate();
        }

        private async Task PlayTutorial_Part2()
        {
            if (TestHelper.IsTestFailed)
            {
                return;
            }

            await TestHelper.ClickGenericButton("Button_Next");

            await TestHelper.WaitUntilOurTurnStarts();
            await TestHelper.WaitUntilInputIsUnblocked();

            for (int i = 0; i < 11; i++)
            {
                await TestHelper.ClickGenericButton("Button_Next");
            }

            await TestHelper.PlayCardFromHandToBoard(new[]
            {
                1
            });

            await TestHelper.ClickGenericButton("Button_Next");

            await TestHelper.LetsThink();
            await TestHelper.LetsThink();

            await TestHelper.PlayCardFromHandToBoard(new[]
            {
                0
            });

            for (int i = 0; i < 12; i++)
            {
                await TestHelper.ClickGenericButton("Button_Next");
            }

            await TestHelper.LetsThink();

            await TestHelper.PlayNonSleepingCardsFromBoardToOpponentPlayer();

            for (int i = 0; i < 5; i++)
            {
                await TestHelper.ClickGenericButton("Button_Next");
            }

            await new WaitForUpdate();
        }
    }
}
