using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Awaken.Contracts.SwapExchangeContract
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class SwapExchangeContractState : ContractState
    {
        
        public SingletonState<Address> Owner { get; set; }
        
        public StringState TargetToken { get; set; }
        
        public SingletonState<Address> Receivor { get; set; }
        
        // Common token symbol and cumulative amount after remove liquity.
        public SingletonState<TokenList> CumulativeTokenList { get; set; }
        
        // The minimum swap threshold for lptoken.
        public Int64State LpTokenThreshold { get; set; }
        // The minimum swap threshold for commonToken.
        public Int64State CommonTokenThreshold { get; set; }
    } 
}