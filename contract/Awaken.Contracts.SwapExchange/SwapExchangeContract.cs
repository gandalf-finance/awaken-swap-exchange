using System;
using System.Linq;
using AElf.Sdk.CSharp;
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

        public override Empty Initialize(InitializeInput input)
        {
            Assert(State.Owner.Value == null, "Contract already Initialized.");
            State.Owner.Value = input.Onwer != null ? input.Onwer : Context.Sender;

            State.Receivor.Value = input.Receivor;
            State.TargetToken.Value = input.TargetToken;
            State.SwapContract.Value = input.SwapContract;
            State.LpTokenContract.Value = input.LpTokenContract;
            State.CommonTokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
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
    }
}