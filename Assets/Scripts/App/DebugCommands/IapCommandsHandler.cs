using System.Collections.Generic;
using System.Linq;
using Loom.Client;
using Loom.ZombieBattleground.BackendCommunication;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.Iap;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OneOf;
using OneOf.Types;
using UnityEngine;
using UnityEngine.Purchasing;
using CardKey = Loom.ZombieBattleground.Data.CardKey;

namespace Loom.ZombieBattleground
{
    public static class IapCommandsHandler
    {
        private static IapMediator _iapMediator;
        private static AuthFiatApiFacade _authFiatApiFacade;
        private static PlasmachainBackendFacade _plasmaChainBackendFacade;
        private static BackendFacade _backendFacade;
        private static BackendDataControlMediator _backendDataControlMediator;

        public static void Initialize()
        {
            _iapMediator = GameClient.Get<IapMediator>();
            _authFiatApiFacade = GameClient.Get<AuthFiatApiFacade>();
            _plasmaChainBackendFacade = GameClient.Get<PlasmachainBackendFacade>();
            _backendFacade = GameClient.Get<BackendFacade>();
            _backendDataControlMediator = GameClient.Get<BackendDataControlMediator>();
        }

        public static async void IapMediatorInitialize()
        {
            OneOf<Success, IapException> result = await _iapMediator.BeginInitialization();
            Debug.Log("Result: " + result);
        }

        public static async void IapMediatorInitiatePurchase(string productId = "booster_pack_1")
        {
            Product product = _iapMediator.Products.Single(p => p.definition.storeSpecificId == productId);
            OneOf<Success, IapPlatformStorePurchaseError, IapPurchaseProcessingError, IapException> result = await _iapMediator.InitiatePurchase(product);
            Debug.Log("Result: " + result);
        }

        public static async void AuthApiGetTransactions()
        {
            List<AuthFiatApiFacade.TransactionReceipt> list = await _authFiatApiFacade.ListPendingTransactions();
            Debug.Log(JsonUtility.PrettyPrint(JsonConvert.SerializeObject(list)));
        }

        public static async void IapOpenPack()
        {
            IReadOnlyList<CardKey> result;
            using (DAppChainClient client = await _plasmaChainBackendFacade.GetConnectedClient())
            {
                result = await _plasmaChainBackendFacade.CallOpenPack(client, Enumerators.MarketplaceCardPackType.Booster);
            }

            JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
            jsonSerializerSettings.Converters.Add(new StringEnumConverter());
            Debug.Log("IapOpenPack Result:\n\n" + JsonConvert.SerializeObject(result, Formatting.Indented, jsonSerializerSettings));
        }

        public static void IapConfirmAllPendingPlatformStorePurchases()
        {
            List<Product> pendingPurchases = _iapMediator.StorePendingPurchases.ToList();

            IIapPlatformStoreFacade platformStoreFacade = GameClient.Get<IIapPlatformStoreFacade>();
            foreach (Product pendingPurchase in pendingPurchases)
            {
                platformStoreFacade.StoreController.ConfirmPendingPurchase(pendingPurchase);
            }
            Debug.Log($"Confirmed {pendingPurchases.Count} pending platform store purchase(s). Please restart the game.");
        }
    }
}
