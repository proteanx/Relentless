using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Loom.ZombieBattleground.Test
{
    public class HordeManipulationTests : BaseIntegrationTest
    {
        [UnityTest]
        [Timeout(500000)]
        public IEnumerator Test_H1_CreateAHordeAndCancel()
        {
            return AsyncTest(async () =>
            {
                TestHelper.SetTestName("Solo - Create a Horde and cancel");

                await TestHelper.ClickGenericButton("Button_Play");

                await TestHelper.AssertIfWentDirectlyToTutorial(
                    TestHelper.GoBackToMainAndPressPlay);

                await TestHelper.AssertCurrentPageName("PlaySelectionPage");
                await TestHelper.ClickGenericButton("Button_SoloMode");
                await TestHelper.AssertCurrentPageName("HordeSelectionPage");
                await TestHelper.ClickGenericButton("Image_BaackgroundGeneral");
                await TestHelper.AssertCurrentPageName("OverlordSelectionPage");
                await TestHelper.PickOverlord("Razu", true);

                await TestHelper.LetsThink();

                await TestHelper.ClickGenericButton("Canvas_BackLayer/Button_Continue");
                await TestHelper.AssertCurrentPageName("HordeEditingPage");
                await TestHelper.ClickGenericButton("Button_Back");
                await TestHelper.RespondToYesNoOverlay(false);
                await TestHelper.AssertCurrentPageName("HordeSelectionPage");

                await new WaitForUpdate();

                TestHelper.TestEndHandler();
            });
        }

        [UnityTest]
        [Timeout(500000)]
        public IEnumerator Test_H2_CreateAHordeAndDraft()
        {
            return AsyncTest(async () =>
            {
                TestHelper.SetTestName("Solo - Create a Horde and draft");

                await TestHelper.ClickGenericButton("Button_Play");

                await TestHelper.AssertIfWentDirectlyToTutorial(
                    TestHelper.GoBackToMainAndPressPlay);

                await TestHelper.AssertCurrentPageName("PlaySelectionPage");
                await TestHelper.ClickGenericButton("Button_SoloMode");
                await TestHelper.AssertCurrentPageName("HordeSelectionPage");

                await TestHelper.SelectAHordeByName("Draft", false);
                if (TestHelper.SelectedHordeIndex != -1)
                {
                    await TestHelper.RemoveAHorde(TestHelper.SelectedHordeIndex);
                }

                await TestHelper.ClickGenericButton("Image_BaackgroundGeneral");
                await TestHelper.AssertCurrentPageName("OverlordSelectionPage");
                await TestHelper.PickOverlord("Razu", true);
                await TestHelper.LetsThink();
                await TestHelper.ClickGenericButton("Canvas_BackLayer/Button_Continue");
                await TestHelper.AssertCurrentPageName("HordeEditingPage");
                await TestHelper.SetDeckTitle("Draft");
                await TestHelper.ClickGenericButton("Button_Back");
                await TestHelper.RespondToYesNoOverlay(true);
                await TestHelper.AssertCurrentPageName("HordeSelectionPage");
                await TestHelper.SelectAHordeByName("Draft", true, "Horde draft isn't displayed.");

                await new WaitForUpdate();

                TestHelper.TestEndHandler();
            });
        }

        [UnityTest]
        [Timeout(500000)]
        public IEnumerator Test_H3_RemoveAllHordesExceptFirst()
        {
            return AsyncTest(async () =>
            {
                TestHelper.SetTestName("Solo - Remove all Hordes except first");

                await TestHelper.ClickGenericButton("Button_Play");

                await TestHelper.AssertIfWentDirectlyToTutorial(
                    TestHelper.GoBackToMainAndPressPlay);

                await TestHelper.AssertCurrentPageName("PlaySelectionPage");
                await TestHelper.ClickGenericButton("Button_SoloMode");
                await TestHelper.AssertCurrentPageName("HordeSelectionPage");
                await TestHelper.RemoveAllHordesExceptDefault();

                await new WaitForUpdate();

                TestHelper.TestEndHandler();
            });
        }

        [UnityTest]
        [Timeout(500000)]
        public IEnumerator Test_H4_CreateARazuHordeAndSave()
        {
            return AsyncTest(async () =>
            {
                TestHelper.SetTestName("Solo - Create a Horde and save");

                await TestHelper.ClickGenericButton("Button_Play");

                await TestHelper.AssertIfWentDirectlyToTutorial(
                    TestHelper.GoBackToMainAndPressPlay);

                await TestHelper.AssertCurrentPageName("PlaySelectionPage");
                await TestHelper.ClickGenericButton("Button_SoloMode");
                await TestHelper.AssertCurrentPageName("HordeSelectionPage");
                await TestHelper.AddRazuHorde();
                await TestHelper.AssertCurrentPageName("HordeSelectionPage");

                await new WaitForUpdate();

                TestHelper.TestEndHandler();
            });
        }

        [UnityTest]
        [Timeout(500000)]
        public IEnumerator Test_H4_CreateKalileHorde()
        {
            return AsyncTest(async () =>
            {
                TestHelper.SetTestName("Solo - Create a Horde and save");

                await TestHelper.ClickGenericButton("Button_Play");

                await TestHelper.AssertIfWentDirectlyToTutorial(
                    TestHelper.GoBackToMainAndPressPlay);

                await TestHelper.AssertCurrentPageName("PlaySelectionPage");
                await TestHelper.ClickGenericButton("Button_SoloMode");
                await TestHelper.AssertCurrentPageName("HordeSelectionPage");
                await TestHelper.AddKalileHorde();
                await TestHelper.AssertCurrentPageName("HordeSelectionPage");

                await new WaitForUpdate();

                TestHelper.TestEndHandler();
            });
        }

        [UnityTest]
        [Timeout(500000)]
        public IEnumerator Test_H5_CreateValashHorde()
        {
            return AsyncTest(async () =>
            {
                TestHelper.SetTestName("Solo - Create a Horde and save");

                await TestHelper.ClickGenericButton("Button_Play");

                await TestHelper.AssertIfWentDirectlyToTutorial(
                    TestHelper.GoBackToMainAndPressPlay);

                await TestHelper.AssertCurrentPageName("PlaySelectionPage");
                await TestHelper.ClickGenericButton("Button_SoloMode");
                await TestHelper.AssertCurrentPageName("HordeSelectionPage");
                await TestHelper.AddValashHorde();
                await TestHelper.AssertCurrentPageName("HordeSelectionPage");

                await new WaitForUpdate();

                TestHelper.TestEndHandler();
            });
        }
    }
}
