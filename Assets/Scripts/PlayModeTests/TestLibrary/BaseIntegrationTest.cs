using System;
using System.Collections;
using System.Threading.Tasks;
using Loom.ZombieBattleground.BackendCommunication;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Loom.ZombieBattleground.Test
{
    public class BaseIntegrationTest
    {
        protected readonly TestHelper TestHelper = TestHelper.Instance;

        #region Setup & TearDown

        [UnitySetUp]
        public virtual IEnumerator PerTestSetup()
        {
            return AsyncTest(async () =>
            {
                await TestHelper.PerTestSetup();

                TestHelper.DebugCheatsConfiguration = new DebugCheatsConfiguration();
            });
        }

        [UnityTearDown]
        public virtual IEnumerator PerTestTearDown()
        {
            return AsyncTest(async () =>
            {
                TestHelper.DebugCheatsConfiguration = new DebugCheatsConfiguration();

                if (false && TestContext.CurrentContext.Test.Name == "TestN_Cleanup")
                {
                    await TestHelper.TearDown_Cleanup();
                }
                else
                {
                    await TestHelper.TearDown_GoBackToMainScreen();
                }

                /*await _testHelper.PerTestTearDown();

                _testHelper.ReportTestTime();*/
            });
        }

        #endregion

        protected IEnumerator AsyncTest(Func<Task> taskFunc)
        {
            return TestHelper.TaskAsIEnumerator(taskFunc);
        }
    }
}
