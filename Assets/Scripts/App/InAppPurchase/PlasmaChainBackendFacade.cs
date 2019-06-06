using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using UnityEngine;
using System.Threading.Tasks;
using Loom.Client;
using Loom.ZombieBattleground.Common;
using Loom.ZombieBattleground.BackendCommunication;
using log4net;
using log4netUnitySupport;
using Loom.Nethereum.ABI.FunctionEncoding.Attributes;
using Loom.ZombieBattleground.Data;
using Newtonsoft.Json;
using OneOf;
using OneOf.Types;

namespace Loom.ZombieBattleground.Iap
{
    public class PlasmaChainBackendFacade : IService
    {
        private static readonly ILog Log = Logging.GetLog(nameof(PlasmaChainBackendFacade));
        private static readonly ILog RpcLog = Logging.GetLog(nameof(PlasmaChainBackendFacade) + "Rpc");

        private const int CardsPerPack = 5;

        private const string RequestPacksMethod = "requestPacks";

        private const string BalanceOfMethod = "balanceOf";

        private const string ApproveMethod = "approve";

        private const string OpenPackMethod = "openBoosterPack";

        private BackendDataControlMediator _backendDataControlMediator;

        private ILoadObjectsManager _loadObjectsManager;

        private IDataManager _dataManager;

        private Dictionary<IapContractType, TextAsset> _abiDictionary;

        private byte[] UserPrivateKey => _backendDataControlMediator.UserDataModel.PrivateKey;

        public byte[] UserPublicKey => CryptoUtils.PublicKeyFromPrivateKey(UserPrivateKey);

        public Address UserPlasmaChainAddress => Address.FromPublicKey(UserPublicKey, PlasmaChainEndpointsContainer.Chainid);

        public void Init()
        {
            _backendDataControlMediator = GameClient.Get<BackendDataControlMediator>();
            _loadObjectsManager = GameClient.Get<ILoadObjectsManager>();
            _dataManager = GameClient.Get<IDataManager>();

            InitAbiTextAssets();
        }

        public void Update() { }

        public void Dispose() { }

        public async Task<DAppChainClient> GetConnectedClient()
        {
            Log.Debug("Creating PlasmaChain client");

            DAppChainClient client = CreateClient();
            client.TxMiddleware = new TxMiddleware(new ITxMiddlewareHandler[]
            {
                new NonceTxMiddleware(UserPublicKey, client), new SignedTxMiddleware(UserPrivateKey)
            });

            if (client.ReadClient.ConnectionState != RpcConnectionState.Connected)
            {
                await client.ReadClient.ConnectAsync();
            }

            if (client.WriteClient.ConnectionState != RpcConnectionState.Connected)
            {
                await client.WriteClient.ConnectAsync();
            }

            return client;
        }

        public async Task<OneOf<Success, IapException>> ClaimPacks(DAppChainClient client, AuthFiatApiFacade.TransactionResponse fiatResponse)
        {
            try
            {
                EvmContract evmContract = GetContract(client, IapContractType.FiatPurchase);
                RequestPacksRequest requestPacksRequest = CreateContractRequestFromTransactionResponse(fiatResponse);
                await CallRequestPacksContract(evmContract, requestPacksRequest);
            }
            catch (Exception e)
            {
                return new IapException($"{nameof(ClaimPacks)} failed", e);
            }

            return new Success();
        }

        public async Task<int> GetPackTypeBalance(DAppChainClient client, Enumerators.MarketplaceCardPackType packTypeId)
        {
            Log.Info($"{nameof(GetPackTypeBalance)}(packTypeId = {packTypeId})");

            EvmContract packTypeContract = GetContract(client, GetPackContractTypeFromId(packTypeId));
            int amount = await packTypeContract.StaticCallSimpleTypeOutputAsync<int>(
                BalanceOfMethod,
                Address.FromPublicKey(UserPublicKey).ToString()
            );

            Log.Info($"{nameof(GetPackTypeBalance)}(packTypeId = {packTypeId}) returned {amount}");
            return amount;
        }

        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public async Task<IReadOnlyList<Card>> CallOpenPack(DAppChainClient client, Enumerators.MarketplaceCardPackType packTypeId)
        {
            Log.Info($"{nameof(GetPackTypeBalance)}(MarketplaceCardPackType packTypeId = {packTypeId})");

            EvmContract cardFaucetContract = GetContract(client, IapContractType.CardFaucet);
            EvmContract packContract = GetContract(client, GetPackContractTypeFromId(packTypeId));

            List<Card> cards = new List<Card>();

            void ContractEventReceived(object sender, EvmChainEventArgs e)
            {
                Log.Info($"{nameof(GetPackTypeBalance)}: received smart contract even " + e.EventName);
                GeneratedCardEvent generatedCardEvent = e.DecodeEventDto<GeneratedCardEvent>();
                Log.Info(
                    $"{nameof(GetPackTypeBalance)}: CardId = {generatedCardEvent.MouldId}, BoosterType ={generatedCardEvent.BoosterType}</color>");

                if (generatedCardEvent.MouldId % 10 != 0)
                {
                    Log.Warn($"{nameof(GetPackTypeBalance)}: Unknown card with raw MouldId {generatedCardEvent.MouldId}");
                    cards.Add(null);
                    return;
                }

                MouldId mouldId = new MouldId((long) (generatedCardEvent.MouldId / 10));
                (bool found, Card card) = _dataManager.CachedCardsLibraryData.TryGetCardFromMouldId(mouldId);
                if (found)
                {
                    Log.Info($"{nameof(GetPackTypeBalance)}: Matching card {card}");
                    cards.Add(card);
                }
                else
                {
                    Log.Warn($"{nameof(GetPackTypeBalance)}: Unknown card with MouldId {mouldId}");
                    cards.Add(null);
                }
            }

            cardFaucetContract.EventReceived += ContractEventReceived;

            await client.SubscribeToEvents();

            const int amountToApprove = 1;
            await packContract.CallAsync(ApproveMethod, PlasmaChainEndpointsContainer.ContractAddressCardFaucet, amountToApprove);
            await cardFaucetContract.CallAsync(OpenPackMethod, packTypeId);

            const double timeout = 15;
            bool timedOut = false;
            double startTime = Utilites.GetTimestamp();

            await new WaitUntil(() =>
            {
                if (Utilites.GetTimestamp() - startTime > timeout)
                {
                    timedOut = true;
                    return true;
                }

                return cards.Count == CardsPerPack;
            });

            if (timedOut)
                throw new TimeoutException();

            cards = cards.Where(card => card != null).ToList();
            Log.Info($"{nameof(GetPackTypeBalance)} returned {Utilites.FormatCallLogList(cards)}");
            return cards;
        }

        private EvmContract GetContract(DAppChainClient client, IapContractType contractType)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            return new EvmContract(
                client,
                Address.FromString(GetContractAddress(contractType), PlasmaChainEndpointsContainer.Chainid),
                UserPlasmaChainAddress,
                _abiDictionary[contractType].text);
        }

        private DAppChainClient CreateClient()
        {
            ILogger logger = new UnityLoggerWrapper(RpcLog);

            IRpcClient writer = RpcClientFactory
                .Configure()
                .WithLogger(logger)
                .WithWebSocket(PlasmaChainEndpointsContainer.WebSocket)
                .Create();

            IRpcClient reader = RpcClientFactory
                .Configure()
                .WithLogger(logger)
                .WithWebSocket(PlasmaChainEndpointsContainer.QueryWS)
                .Create();

            DAppChainClientConfiguration clientConfiguration = new DAppChainClientConfiguration
            {
                AutoReconnect = false,
                CallTimeout = Constants.PlasmachainCallTimeout,
                StaticCallTimeout = Constants.PlasmachainCallTimeout
            };

            return new LoggingDAppChainClient(writer, reader, clientConfiguration)
            {
                Logger = logger
            };
        }

        private void InitAbiTextAssets()
        {
            _abiDictionary = new Dictionary<IapContractType, TextAsset>
            {
                {
                    IapContractType.FiatPurchase, _loadObjectsManager.GetObjectByPath<TextAsset>("Data/abi/FiatPurchaseABI")
                },
                {
                    IapContractType.CardFaucet, _loadObjectsManager.GetObjectByPath<TextAsset>("Data/abi/CardFaucetABI")
                }
            };

            (IapContractType contractType, Enumerators.MarketplaceCardPackType cardPackType)[] contractTypeToCardPackType =
            {
                (IapContractType.BoosterPack, Enumerators.MarketplaceCardPackType.Booster),
                (IapContractType.SuperPack, Enumerators.MarketplaceCardPackType.Super),
                (IapContractType.AirPack, Enumerators.MarketplaceCardPackType.Air),
                (IapContractType.EarthPack, Enumerators.MarketplaceCardPackType.Earth),
                (IapContractType.FirePack, Enumerators.MarketplaceCardPackType.Fire),
                (IapContractType.LifePack, Enumerators.MarketplaceCardPackType.Life),
                (IapContractType.ToxicPack, Enumerators.MarketplaceCardPackType.Toxic),
                (IapContractType.WaterPack, Enumerators.MarketplaceCardPackType.Water),
                (IapContractType.SmallPack, Enumerators.MarketplaceCardPackType.Small),
                (IapContractType.MinionPack, Enumerators.MarketplaceCardPackType.Minion)
            };
            for (int i = 0; i < contractTypeToCardPackType.Length; ++i)
            {
                _abiDictionary.Add(
                    contractTypeToCardPackType[i].contractType,
                    _loadObjectsManager.GetObjectByPath<TextAsset>($"Data/abi/{contractTypeToCardPackType[i].cardPackType.ToString()}PackABI")
                );
            }
        }

        private string GetContractAddress(IapContractType contractType)
        {
            switch (contractType)
            {
                case IapContractType.FiatPurchase:
                    return PlasmaChainEndpointsContainer.ContractAddressFiatPurchase;
                case IapContractType.CardFaucet:
                    return PlasmaChainEndpointsContainer.ContractAddressCardFaucet;
                case IapContractType.BoosterPack:
                    return PlasmaChainEndpointsContainer.ContractAddressBoosterPack;
                case IapContractType.SuperPack:
                    return PlasmaChainEndpointsContainer.ContractAddressSuperPack;
                case IapContractType.AirPack:
                    return PlasmaChainEndpointsContainer.ContractAddressAirPack;
                case IapContractType.EarthPack:
                    return PlasmaChainEndpointsContainer.ContractAddressEarthPack;
                case IapContractType.FirePack:
                    return PlasmaChainEndpointsContainer.ContractAddressFirePack;
                case IapContractType.LifePack:
                    return PlasmaChainEndpointsContainer.ContractAddressLifePack;
                case IapContractType.ToxicPack:
                    return PlasmaChainEndpointsContainer.ContractAddressToxicPack;
                case IapContractType.WaterPack:
                    return PlasmaChainEndpointsContainer.ContractAddressWaterPack;
                case IapContractType.SmallPack:
                    return PlasmaChainEndpointsContainer.ContractAddressSmallPack;
                case IapContractType.MinionPack:
                    return PlasmaChainEndpointsContainer.ContractAddressMinionPack;
                default:
                    throw new ArgumentOutOfRangeException(nameof(contractType), contractType, null);
            }
        }

        private async Task CallRequestPacksContract(EvmContract contract, RequestPacksRequest requestPacksRequest)
        {
            Log.Info($"{nameof(CallRequestPacksContract)}, ContractRequest:\n" + JsonConvert.SerializeObject(requestPacksRequest));
            await contract.CallAsync(
                RequestPacksMethod,
                requestPacksRequest.UserId,
                requestPacksRequest.r,
                requestPacksRequest.s,
                requestPacksRequest.v,
                requestPacksRequest.hash,
                requestPacksRequest.amount,
                requestPacksRequest.TxID
            );
            Log.Info($"Smart contract method [{RequestPacksMethod}] finished executing.");
        }

        private RequestPacksRequest CreateContractRequestFromTransactionResponse(AuthFiatApiFacade.TransactionResponse fiatResponse)
        {
            string r = fiatResponse.VerifyHash.signature.SubstringIndexed(2, 66);
            string s = fiatResponse.VerifyHash.signature.SubstringIndexed(66, 130);
            string v = fiatResponse.VerifyHash.signature.SubstringIndexed(130, 132);

            RequestPacksRequest request = new RequestPacksRequest
            {
                UserId = fiatResponse.UserId,
                r = CryptoUtils.HexStringToBytes(r),
                s = CryptoUtils.HexStringToBytes(s),
                v = (byte) Int32.Parse(v, NumberStyles.AllowHexSpecifier),
                hash = CryptoUtils.HexStringToBytes(fiatResponse.VerifyHash.hash),
                amount = new[]
                {
                    fiatResponse.Booster,
                    fiatResponse.Super,
                    fiatResponse.Air,
                    fiatResponse.Earth,
                    fiatResponse.Fire,
                    fiatResponse.Life,
                    fiatResponse.Toxic,
                    fiatResponse.Water,
                    fiatResponse.Small,
                    fiatResponse.Minion,
                    fiatResponse.Binance
                },
                TxID = fiatResponse.TxID
            };
            return request;
        }

        private IapContractType GetPackContractTypeFromId(Enumerators.MarketplaceCardPackType packId)
        {
            switch (packId)
            {
                case Enumerators.MarketplaceCardPackType.Booster:
                    return IapContractType.BoosterPack;
                case Enumerators.MarketplaceCardPackType.Super:
                    return IapContractType.SuperPack;
                case Enumerators.MarketplaceCardPackType.Air:
                    return IapContractType.AirPack;
                case Enumerators.MarketplaceCardPackType.Earth:
                    return IapContractType.EarthPack;
                case Enumerators.MarketplaceCardPackType.Fire:
                    return IapContractType.FirePack;
                case Enumerators.MarketplaceCardPackType.Life:
                    return IapContractType.LifePack;
                case Enumerators.MarketplaceCardPackType.Toxic:
                    return IapContractType.ToxicPack;
                case Enumerators.MarketplaceCardPackType.Water:
                    return IapContractType.WaterPack;
                case Enumerators.MarketplaceCardPackType.Small:
                    return IapContractType.SmallPack;
                case Enumerators.MarketplaceCardPackType.Minion:
                    return IapContractType.MinionPack;
                default:
                    throw new Exception($"Not found ContractType from pack id {packId}");
            }
        }

        private class LoggingDAppChainClient : DAppChainClient
        {
            public LoggingDAppChainClient(
                IRpcClient writeClient,
                IRpcClient readClient,
                DAppChainClientConfiguration configuration = null,
                IDAppChainClientCallExecutor callExecutor = null)
                : base(writeClient, readClient, configuration, callExecutor) { }

            public override void Dispose()
            {
                base.Dispose();
                Log.Debug("Disposing PlasmaChain client");
            }
        }

        [Event("GeneratedCard")]
        private class GeneratedCardEvent
        {
            [Parameter("uint256", "cardId")]
            public BigInteger MouldId { get; set; }

            [Parameter("uint256", "boosterType", 2)]
            public BigInteger BoosterType { get; set; }
        }

        private struct RequestPacksRequest
        {
            public int UserId;
            public byte[] r;
            public byte[] s;
            public byte v;
            public byte[] hash;
            public int[] amount;
            public int TxID;
        }
    }
}
