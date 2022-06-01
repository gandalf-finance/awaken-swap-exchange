using System;
using System.Linq;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Awaken.Contracts.SwapExchangeContract
{
    /// <summary>
    /// The C# implementation of the contract defined in swap_exchange_contract.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public partial class SwapExchangeContract : SwapExchangeContractContainer.SwapExchangeContractBase
    {
        private const string PairPrefix = "ALP";
        private const string ExpansionCoefficient = "1000000000000000000";
        public const long DefaultTargetTokenThreshold = 500_000000L;
        
        public override Empty Initialize(InitializeInput input)
        {
            Assert(State.Owner.Value == null, "Contract already Initialized.");
            State.Owner.Value = input.Onwer != null ? input.Onwer : Context.Sender;
            State.Receivor.Value = input.Receivor;
            Assert(!string.IsNullOrEmpty(input.TargetToken), "Target token not config.");
            State.TargetToken.Value = input.TargetToken;
            State.SwapContract.Value = input.SwapContract;
            State.LpTokenContract.Value = input.LpTokenContract;
            State.CommonTokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.TargetTokenThreshold.Value = input.TargetTokenThreshold > 0
                ? input.TargetTokenThreshold
                : DefaultTargetTokenThreshold;
            return new Empty();
        }


        private void OnlyOwner()
        {
            Assert(State.Owner.Value != null, "Contract not initialized.");
            Assert(Context.Sender == State.Owner.Value, "Not permission.");
        }

        private string ExtractTokenPairFromSymbol(string symbol)
        {
            Assert(!string.IsNullOrEmpty(symbol), "Symbol blank.");
            // ReSharper disable once PossibleNullReferenceException
            return symbol.StartsWith(PairPrefix)
                ? symbol.Substring(symbol.IndexOf(PairPrefix, StringComparison.Ordinal) + PairPrefix.Length).Trim()
                : symbol.Trim();
        }

        private string[] ExtractTokensFromTokenPair(string tokenPair)
        {
            Assert(tokenPair.Contains("-") && tokenPair.Count(c => c == '-') == 1, $"Invalid TokenPair {tokenPair}.");
            return SortSymbols(tokenPair.Split('-'));
        }

        private string[] SortSymbols(params string[] symbols)
        {
            Assert(symbols.Length == 2, "Invalid symbols for sorting.");
            return symbols.OrderBy(s => s).ToArray();
        }


        private void OnlySelf()
        {
            Assert(Context.Self == Context.Sender, "No permission.");
        }

        private long BigIntValueToLong(BigIntValue inValue)
        {
            long amount = 0;
            if (inValue != null && !long.TryParse(inValue.Value, out amount))
            {
                throw new AssertionException($"Fail to parse {inValue.Value} to long.");
            }

            return amount;
        }
    }
}