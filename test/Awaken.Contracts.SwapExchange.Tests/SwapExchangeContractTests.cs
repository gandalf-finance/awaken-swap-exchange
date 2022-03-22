using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using Awaken.Contracts.SwapExchangeContract;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Awaken.Contracts.SwapExchange
{
    public class SwapExchangeContractTests : SwapExchangeContractTestBase
    {

        [Fact]
        public async Task Test()
        {
            await Initialize();
        }
        
        [Fact]
        public async Task Set_Tests()
        {
            await Initialize();
            var ownerSwapExchangeStub = GetSwapExchangeContractStub(OwnerPair);
            var targetToken=await ownerSwapExchangeStub.TargetToken.CallAsync(new Empty());
            targetToken.Value.ShouldBe(SymbolUsdt);
            await ownerSwapExchangeStub.SetTargetToken.SendAsync(new StringValue
            {
                Value = SymbolElff
            });
            targetToken=await ownerSwapExchangeStub.TargetToken.CallAsync(new Empty());
            targetToken.Value.ShouldBe(SymbolElff);

            var receivor = await ownerSwapExchangeStub.Receivor.CallAsync(new Empty());
            receivor.ShouldBe(receivor);
            await ownerSwapExchangeStub.SetReceivor.SendAsync(Tom);
            receivor = await ownerSwapExchangeStub.Receivor.CallAsync(new Empty());
            receivor.ShouldBe(Tom);
        }
        
        [Fact]
         public async Task Swap_Common_Token_Path_By_Common_Token_Test()
        {
            await Initialize();
            var ownerSwapExchangeContractStub = GetSwapExchangeContractStub(OwnerPair);
            var receiverCommonStub = GetCommonTokenContractStub(ReceiverPair);
            var path = new Dictionary<string, Path>();
            var swapTokenList = new TokenList();
            path[SymbolAave] = new Path
            {
                Value = {SymbolAave,SymbolLink,SymbolUsdt}
            };
            path[SymbolElff] = new Path
            {
                Value = { SymbolElff,SymbolLink,SymbolUsdt}
            };

            path[SymbolLink] = new Path
            {
                Value = {SymbolLink,SymbolUsdt}
            };
             var ownerCommonTokenStub = GetCommonTokenContractStub(OwnerPair);

            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = SymbolAave,
                Amount = 10_00000000
            });
            
            await ownerCommonTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 10_00000000,
                Spender = DAppContractAddress,
                Symbol = SymbolAave
            });
             
            
            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = SymbolLink,
                Amount = 20_00000000
            });
            await ownerCommonTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 20_00000000,
                Spender = DAppContractAddress,
                Symbol = SymbolLink
            });
            
            
            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = SymbolElff,
                Amount = 40_000000000
            });
            await ownerCommonTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 40_000000000,
                Spender = DAppContractAddress,
                Symbol = SymbolElff
            });

            var balanceReceiverBefore = await receiverCommonStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Receiver,
                Symbol = SymbolUsdt
            });
            balanceReceiverBefore.Balance.ShouldBe(0);

            await ownerSwapExchangeContractStub.SwapCommonTokens.SendAsync(new SwapTokensInput
            {
                PathMap = { path},
                SwapTokenList = swapTokenList
            });
            
            
            var balanceReceiverAfter = await receiverCommonStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Receiver,
                Symbol = SymbolUsdt
            });
            balanceReceiverAfter.Balance.ShouldBeGreaterThan(0);
            balanceReceiverAfter.Balance.ShouldBe(2201801505L);
        }
        
        [Fact]
        public async Task Swap_Common_Token_Path_By_Pair_Test()
        {
            await Initialize();
            var ownerSwapExchangeContractStub = GetSwapExchangeContractStub(OwnerPair);
            var receiverCommonStub = GetCommonTokenContractStub(ReceiverPair);
            var path = new Dictionary<string, Path>();
            var swapTokenList = new TokenList();
            path[SymbolAave] = new Path
            {
                Value = {$"ALP {SymbolAave}-{SymbolLink}", $"{SymbolLink}-{SymbolUsdt}"}
            };
            path[SymbolElff] = new Path
            {
                Value = { $"ALP {SymbolElff}-{SymbolLink}",$"{SymbolLink}-{SymbolUsdt}"}
            };

            path[SymbolLink] = new Path
            {
                Value = {$"{SymbolLink}-{SymbolUsdt}"}
            };
            
            var ownerCommonTokenStub = GetCommonTokenContractStub(OwnerPair);
            
            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = SymbolAave,
                Amount = 10_00000000
            });
            
            await ownerCommonTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 10_00000000,
                Spender = DAppContractAddress,
                Symbol = SymbolAave
            });
             
            
            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = SymbolLink,
                Amount = 20_00000000
            });
            await ownerCommonTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 20_00000000,
                Spender = DAppContractAddress,
                Symbol = SymbolLink
            });
            
            
            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = SymbolElff,
                Amount = 40_000000000
            });
            await ownerCommonTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 40_000000000,
                Spender = DAppContractAddress,
                Symbol = SymbolElff
            });

            var balanceReceiverBefore = await receiverCommonStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Receiver,
                Symbol = SymbolUsdt
            });
            balanceReceiverBefore.Balance.ShouldBe(0);

            await ownerSwapExchangeContractStub.SwapCommonTokens.SendAsync(new SwapTokensInput
            {
                PathMap = { path},
                SwapTokenList = swapTokenList
            });
            
            
            var balanceReceiverAfter = await receiverCommonStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Receiver,
                Symbol = SymbolUsdt
            });
            balanceReceiverAfter.Balance.ShouldBeGreaterThan(0);
            balanceReceiverAfter.Balance.ShouldBe(2201801505L);
        }

        [Fact]
        public async Task Swap_LpToken_Test()
        {
            await Initialize();
            var ownerSwapExchangeContractStub = GetSwapExchangeContractStub(OwnerPair);
            var receiverCommonStub = GetCommonTokenContractStub(ReceiverPair);
            var path = new Dictionary<string,Path>();
            var swapTokenList = new TokenList();
            path[SymbolAave] = new Path
            {
                Value = {$"ALP {SymbolAave}-{SymbolLink}", $"ALP {SymbolLink}-{SymbolUsdt}"}
            };
            
            path[SymbolElff] = new Path
            {
                Value = { $"{SymbolElff}-{SymbolLink}",$"{SymbolLink}-{SymbolUsdt}"}
            };

            path[SymbolLink] = new Path
            {
                Value = {$"{SymbolLink}-{SymbolUsdt}"}
            };
            
            var ownerLpTokenStub = GetLpTokenContractStub(OwnerPair);
            
            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = GetTokenPairSymbol(SymbolLink,SymbolElff),
                Amount = 20_00000000
            });
            await ownerLpTokenStub.Approve.SendAsync(new Token.ApproveInput
            {
                Amount = 20_00000000,
                Spender = DAppContractAddress,
                Symbol = GetTokenPairSymbol(SymbolLink, SymbolElff)
            });

            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                Amount = 10_00000000,
                TokenSymbol = GetTokenPairSymbol(SymbolLink, SymbolUsdt)
            });
            
            await ownerLpTokenStub.Approve.SendAsync(new Token.ApproveInput
            {
                Amount = 10_00000000,
                Spender = DAppContractAddress,
                Symbol = GetTokenPairSymbol(SymbolLink, SymbolUsdt)
            });

            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = GetTokenPairSymbol(SymbolAave, SymbolLink),
                Amount = 100_00000000
            });
            
            await ownerLpTokenStub.Approve.SendAsync(new Token.ApproveInput
            {
                Amount = 100_00000000,
                Spender = DAppContractAddress,
                Symbol = GetTokenPairSymbol(SymbolAave, SymbolLink)
            });

            var balanceReceiverBefore = await receiverCommonStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Receiver,
                Symbol = SymbolUsdt
            });
            balanceReceiverBefore.Balance.ShouldBe(0);

            await ownerSwapExchangeContractStub.SwapLpTokens.SendAsync(new SwapTokensInput
            {
                PathMap = { path},
                SwapTokenList = swapTokenList
            });

            var  balanceReceiverAfter=await receiverCommonStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Receiver,
                Symbol = SymbolUsdt
            });
            balanceReceiverAfter.Balance.ShouldBeGreaterThan(0);
            balanceReceiverAfter.Balance.ShouldBe(3964253259L);
        }
    }
} 