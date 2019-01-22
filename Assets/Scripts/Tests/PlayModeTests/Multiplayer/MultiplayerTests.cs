using System;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loom.ZombieBattleground.BackendCommunication;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Data;
using UnityEngine.TestTools;

namespace Loom.ZombieBattleground.Test
{
    public class MultiplayerTests : BaseIntegrationTest
    {
        [UnityTest]
        [Timeout(150 * 1000 * TestHelper.TestTimeScale)]
        public IEnumerator PlayScenarioGame1()
        {
            return AsyncTest(async () =>
            {
                Deck deck = new Deck(
                    0,
                    0,
                    "test deck",
                    new List<DeckCardData>
                    {
                        new DeckCardData("Slab", 30)
                    },
                    Enumerators.OverlordSkill.NONE,
                    Enumerators.OverlordSkill.NONE
                );

                InstanceId playerSlabId = new InstanceId(36);
                InstanceId opponentSlabId = new InstanceId(2);
                IReadOnlyList<Action<QueueProxyPlayerActionTestProxy>> turns = new Action<QueueProxyPlayerActionTestProxy>[]
                   {
                       opponent => {},
                       player => {},
                       opponent => {},
                       player => {},
                       opponent => {},
                       player => {},
                       opponent => {},
                       player => {},
                       opponent => {},
                       player => player.CardPlay(playerSlabId, 0),
                       opponent => opponent.CardPlay(opponentSlabId, 0),
                       player => player.CardAttack(playerSlabId, Enumerators.AffectObjectType.Player, TestHelper.GetOpponentPlayer().InstanceId),
                       opponent => opponent.CardAttack(opponentSlabId, Enumerators.AffectObjectType.Player, TestHelper.GetCurrentPlayer().InstanceId),
                       player => player.CardAttack(playerSlabId, Enumerators.AffectObjectType.Player, TestHelper.GetOpponentPlayer().InstanceId),
                       opponent => opponent.CardAttack(opponentSlabId, Enumerators.AffectObjectType.Player, TestHelper.GetCurrentPlayer().InstanceId),
                       player => player.CardAttack(playerSlabId, Enumerators.AffectObjectType.Player,TestHelper.GetOpponentPlayer().InstanceId),
                       opponent => {},
                       player => player.CardAttack(playerSlabId, Enumerators.AffectObjectType.Player, TestHelper.GetOpponentPlayer().InstanceId),
                       opponent => {},
                       player => player.CardAttack(playerSlabId, Enumerators.AffectObjectType.Player, TestHelper.GetOpponentPlayer().InstanceId),
                       opponent => {},
                       player => player.CardAttack(playerSlabId, Enumerators.AffectObjectType.Player, TestHelper.GetOpponentPlayer().InstanceId),
                       opponent => {},
                       player => player.CardAttack(playerSlabId, Enumerators.AffectObjectType.Player, TestHelper.GetOpponentPlayer().InstanceId),
                       opponent => {},
                       player => player.CardAttack(playerSlabId, Enumerators.AffectObjectType.Player, TestHelper.GetOpponentPlayer().InstanceId),
                       opponent => {},
                       player => player.CardAttack(playerSlabId, Enumerators.AffectObjectType.Player, TestHelper.GetOpponentPlayer().InstanceId),
                       opponent => {},
                       player => player.CardAttack(playerSlabId, Enumerators.AffectObjectType.Player, TestHelper.GetOpponentPlayer().InstanceId),
                   };

                await GenericPvPTest(
                    turns,
                    () =>
                    {
                        TestHelper.DebugCheats.ForceFirstTurnUserId = TestHelper.GetOpponentDebugClient().UserDataModel.UserId;
                        TestHelper.DebugCheats.UseCustomDeck = true;
                        TestHelper.DebugCheats.CustomDeck = deck;
                        TestHelper.DebugCheats.DisableDeckShuffle = true;
                    },
                    cheats =>
                    {
                        cheats.UseCustomDeck = true;
                        cheats.CustomDeck = deck;
                    }
                );
            });
        }

        [UnityTest]
        [Timeout(150 * 1000 * TestHelper.TestTimeScale)]
        public IEnumerator Cynderman()
        {
            return AsyncTest(async () =>
            {
                PvPTestHelper pvpTestHelper = new PvPTestHelper();

                Deck opponentDeck = new Deck(
                    0,
                    0,
                    "test deck",
                    new List<DeckCardData>
                    {
                        new DeckCardData("Cynderman", 2),
                        new DeckCardData("Slab", 2)
                    },
                    Enumerators.OverlordSkill.NONE,
                    Enumerators.OverlordSkill.NONE
                );

                Deck localDeck = new Deck(
                    0,
                    0,
                    "test deck2",
                    new List<DeckCardData>
                    {
                        new DeckCardData("Cynderman", 2),
                        new DeckCardData("Slab", 2)
                    },
                    Enumerators.OverlordSkill.NONE,
                    Enumerators.OverlordSkill.NONE
                );

                InstanceId playerSlabId = new InstanceId(8);
                InstanceId opponentSlabId = new InstanceId(4);
                InstanceId playerCyndermanId = new InstanceId(6);
                InstanceId opponentCyndermanId = new InstanceId(2);
                IReadOnlyList<Action<QueueProxyPlayerActionTestProxy>> turns = new Action<QueueProxyPlayerActionTestProxy>[]
                   {
                       opponent => {},
                       player => {},
                       opponent => {},
                       player => {},
                       opponent => {},
                       player => player.CardPlay(playerSlabId, 0),
                       opponent =>
                       {
                           opponent.CardPlay(opponentSlabId, 0);
                           opponent.CardPlay(opponentCyndermanId, 0);
                           opponent.CardAbilityUsed(
                               opponentCyndermanId,
                               Enumerators.AbilityType.DAMAGE_TARGET,
                               new List<ParametrizedAbilityBoardObject>
                               {
                                   new ParametrizedAbilityBoardObject(TestHelper.BattlegroundController.GetBoardObjectById(playerSlabId))
                               }
                           );
                       },
                       player =>
                       {
                           player.CardPlay(playerCyndermanId, 0, opponentCyndermanId);
                       },
                   };

                await GenericPvPTest(
                    turns,
                    () =>
                    {
                        TestHelper.DebugCheats.ForceFirstTurnUserId = TestHelper.GetOpponentDebugClient().UserDataModel.UserId;
                        TestHelper.DebugCheats.UseCustomDeck = true;
                        TestHelper.DebugCheats.CustomDeck = localDeck;
                        TestHelper.DebugCheats.DisableDeckShuffle = true;
                        TestHelper.DebugCheats.IgnoreGooRequirements = true;
                    },
                    cheats =>
                    {
                        cheats.UseCustomDeck = true;
                        cheats.CustomDeck = opponentDeck;
                    }
                );

                Assert.AreEqual(2, ((BoardUnitModel) TestHelper.BattlegroundController.GetBoardObjectById(playerSlabId)).CurrentHp);
                Assert.AreEqual(2, ((BoardUnitModel) TestHelper.BattlegroundController.GetBoardObjectById(opponentSlabId)).CurrentHp);
            });
        }

        [UnityTest]
        [Timeout(150 * 1000 * TestHelper.TestTimeScale)]
        public IEnumerator CorrectCardDraw()
        {
            return AsyncTest(async () =>
            {
                await StartOnlineMatch();
                TestHelper.DebugCheats.ForceFirstTurnUserId = TestHelper.BackendDataControlMediator.UserDataModel.UserId;

                IReadOnlyList<Action<QueueProxyPlayerActionTestProxy>> turns = new Action<QueueProxyPlayerActionTestProxy>[]
                {
                    player => {},
                    opponent => {},
                    player => {},
                };

                MatchScenarioPlayer matchScenarioPlayer = new MatchScenarioPlayer(TestHelper, turns);
                await TestHelper.MatchmakeOpponentDebugClient();

                await matchScenarioPlayer.Play();

                await TestHelper.ClickGenericButton("Button_Settings");
                await TestHelper.ClickGenericButton("Button_QuitToMainMenu");
                await TestHelper.RespondToYesNoOverlay(true);
            });
        }

        private async Task GenericPvPTest(IReadOnlyList<Action<QueueProxyPlayerActionTestProxy>> turns, Action setupAction, Action<DebugCheatsConfiguration> modifyOpponentDebugCheats)
        {
            await TestHelper.CreateAndConnectOpponentDebugClient();

            setupAction?.Invoke();

            await StartOnlineMatch(createOpponent: false);

            MatchScenarioPlayer matchScenarioPlayer = new MatchScenarioPlayer(TestHelper, turns);
            await TestHelper.MatchmakeOpponentDebugClient(modifyOpponentDebugCheats);
            await TestHelper.WaitUntilPlayerOrderIsDecided();

            await matchScenarioPlayer.Play();
        }
    }
}
