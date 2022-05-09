using System.Globalization;
using Google.Protobuf;
using DeFi_Strategies.Helpers;
using NLog;
using TronNet;
using TronNet.ABI;
using TronNet.ABI.FunctionEncoding;
using TronNet.ABI.FunctionEncoding.Attributes;
using TronNet.ABI.Model;
using TronNet.Contracts;
using TronNet.Crypto;
using TronNet.Protocol;
using BigInteger = System.Numerics.BigInteger;

namespace DeFi_Strategies.Tron.CompoundUSDD
{
    public class SuperGaugeContractClient
    {
        private readonly IWalletClient _walletClient;
        private readonly ITransactionClient _transactionClient;
        private readonly string contractAddress;
        private readonly string accountAddress;
        private readonly string privateKey;

        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        public SuperGaugeContractClient(IWalletClient walletClient, ITransactionClient transactionClient, string contractAddress, string accountAddress, string privateKey)
        {
            _walletClient = walletClient;
            _transactionClient = transactionClient;
            this.contractAddress = contractAddress;
            this.accountAddress = accountAddress;
            this.privateKey = privateKey;
        }

        [Function("claim_rewards")]
        public class ClaimRewardsFunction : FunctionMessage
        {
        }

        public async Task<string> ClaimRewardsAsync()
        {
            var contractAddressBytes = Base58Encoder.DecodeFromBase58Check(contractAddress);
            var myAddressBytes = Base58Encoder.DecodeFromBase58Check(accountAddress);

            FunctionABI? functionABI = ABITypedRegistry.GetFunctionABI<ClaimRewardsFunction>();

            try
            {
                ClaimRewardsFunction claim = new ClaimRewardsFunction();

                var encodedHex = new FunctionCallEncoder().EncodeRequest(claim, functionABI.Sha3Signature);

                var transaction = await TronHelpers.TriggerContractAsync(this._walletClient, contractAddressBytes, myAddressBytes, encodedHex);

                transaction.RawData.FeeLimit = 400 * 1000000L;

                if (transaction.Ret.Count > 0 && transaction.Ret[0].Ret == Transaction.Types.Result.Types.code.Failed)
                {
                    return null;
                }

                var transSign = _transactionClient.GetTransactionSign(transaction, privateKey);

                await _transactionClient.BroadcastTransactionAsync(transSign);

                return transSign.GetTxid();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, ex.Message);
                return null;
            }
        }

        [Function("claimable_reward_for", "uint256")]
        public class ClaimableRewardsFunction : FunctionMessage
        {
            [Parameter("address", "addr_address", 1)]
            public string addr_address { get; set; }
        }

        public async Task<double> GetClaimableRewards()
        {
            var contractAddressBytes = Base58Encoder.DecodeFromBase58Check(contractAddress);
            var ownerAddressBytes = Base58Encoder.DecodeFromBase58Check(accountAddress);
            var wallet = _walletClient.GetProtocol();
            var functionABI = ABITypedRegistry.GetFunctionABI<ClaimableRewardsFunction>();

            var addressBytes = new byte[20];
            Array.Copy(ownerAddressBytes, 1, addressBytes, 0, addressBytes.Length);

            var addressBytesHex = "0x" + addressBytes.ToHex();

            var claimableRewards = new ClaimableRewardsFunction { addr_address = addressBytesHex };

            var encodedHex = new FunctionCallEncoder().EncodeRequest(claimableRewards, functionABI.Sha3Signature);

            var trigger = new TriggerSmartContract
            {
                ContractAddress = ByteString.CopyFrom(contractAddressBytes),
                OwnerAddress = ByteString.CopyFrom(ownerAddressBytes),
                Data = ByteString.CopyFrom(encodedHex.HexToByteArray()),
            };

            var transactionExtention = await wallet.TriggerConstantContractAsync(trigger, headers: _walletClient.GetHeaders());

            if (!transactionExtention.Result.Result)
            {
                throw new Exception(transactionExtention.Result.Message.ToStringUtf8());
            }
            if (transactionExtention.ConstantResult.Count == 0)
            {
                throw new Exception($"result error, ConstantResult length=0.");
            }

            var output = transactionExtention.ConstantResult[0].ToByteArray().ToHex();

            var num = BigInteger.Parse(output, NumberStyles.HexNumber);

            var claimableUSDD = num.DivideToDouble(1000000000000000000);

            return claimableUSDD;
        }

        public async Task<string> DepositLPTokensAsync(decimal LPTokensAmount)
        {
            var contractAddressBytes = Base58Encoder.DecodeFromBase58Check(contractAddress);
            var myAddressBytes = Base58Encoder.DecodeFromBase58Check(accountAddress);

            FunctionABI? functionABI = ABITypedRegistry.GetFunctionABI<DepositLPTokensFunction>();

            try
            {
                DepositLPTokensFunction deposit = new DepositLPTokensFunction()
                {
                    value = (BigInteger)(LPTokensAmount * 1000000000000000000)
                };

                var encodedHex = new FunctionCallEncoder().EncodeRequest(deposit, functionABI.Sha3Signature);

                var transaction = await TronHelpers.TriggerContractAsync(this._walletClient, contractAddressBytes, myAddressBytes, encodedHex);

                transaction.RawData.FeeLimit = 400 * 1000000L;

                if (transaction.Ret.Count > 0 && transaction.Ret[0].Ret == Transaction.Types.Result.Types.code.Failed)
                {
                    return null;
                }

                var transSign = _transactionClient.GetTransactionSign(transaction, privateKey);

                await _transactionClient.BroadcastTransactionAsync(transSign);

                return transSign.GetTxid();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, ex.Message);
                return null;
            }
        }

        [Function("deposit")]
        public class DepositLPTokensFunction : FunctionMessage
        {
            [Parameter("uint256", "_value", 1)]
            public BigInteger value { get; set; }
        }
    }
}
