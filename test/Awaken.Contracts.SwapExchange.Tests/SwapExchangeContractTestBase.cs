using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProvider;
using AElf.Contracts.MultiToken;
using AElf.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.CSharp.Core;
using AElf.Types;
using Awaken.Contracts.SwapExchangeContract;
using Awaken.Contracts.Swap;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;
using CreateInput = AElf.Contracts.MultiToken.CreateInput;
using GetBalanceInput = AElf.Contracts.MultiToken.GetBalanceInput;
using InitializeInput = Awaken.Contracts.SwapExchangeContract.InitializeInput;
using IssueInput = AElf.Contracts.MultiToken.IssueInput;
using TokenContractContainer = AElf.Contracts.MultiToken.TokenContractContainer;
using TransferInput = AElf.Contracts.MultiToken.TransferInput;

namespace Awaken.Contracts.SwapExchange
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public class SwapExchangeContractTestBase : DAppContractTestBase<SwapExchangeContractTestModule>
    {
        internal Address Owner;
        internal ECKeyPair OwnerPair;

        internal Address Tom;
        internal ECKeyPair TomPair;

        internal Address Kitty;
        internal ECKeyPair KittyPair;

        internal Address Receiver;
        internal ECKeyPair ReceiverPair;
        
        // constant
        internal const string SymbolUsdt = "USDT";
        internal const string SymbolElff = "ELFF";
        internal const string SymbolAave = "AAVE";
        internal const string SymbolLink = "LINK";
        internal const long TotalSupply = 1000000_00000000;
        internal const string ExpansionCoefficient = "1000000000000000000";
        
        private async Task AddLiquidity()
        {
            var commonTokenTomStub = GetCommonTokenContractStub(TomPair);
            var lpTokenTomStub = GetLpTokenContractStub(TomPair);
            // approve first
            await commonTokenTomStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 50_00000000,
                Symbol = SymbolLink,
                Spender = SwapContractAddress
            });

            await commonTokenTomStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 100_00000000,
                Spender = SwapContractAddress,
                Symbol = SymbolElff
            });
                
            var tomSwapStub = GetSwapContractStub(TomPair);
            await tomSwapStub.AddLiquidity.SendAsync(new AddLiquidityInput
            {
                AmountADesired = 50_00000000,
                AmountAMin = 1,
                AmountBDesired = 100_00000000,
                AmountBMin = 1,
                To = Tom,
                Deadline = Timestamp.FromDateTime(DateTime.UtcNow.Add(new TimeSpan(0, 0, 3))),
                SymbolA = SymbolLink,
                SymbolB = SymbolElff
            });


            var pair1Balance = await lpTokenTomStub.GetBalance.CallAsync(new Token.GetBalanceInput
            {
                Owner = Tom,
                Symbol = GetTokenPairSymbol(SymbolLink, SymbolElff)
            });
            pair1Balance.Amount.ShouldBe(7071067811);

            await lpTokenTomStub.Transfer.SendAsync(new Token.TransferInput
            {
                Amount = 50_00000000,
                Symbol = GetTokenPairSymbol(SymbolLink, SymbolElff),
                To = Owner
            });
            
            await commonTokenTomStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 100_00000000,
                Spender = SwapContractAddress,
                Symbol = SymbolLink
            });
            await commonTokenTomStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 50_00000000,
                Spender = SwapContractAddress,
                Symbol = SymbolUsdt
            });

            await tomSwapStub.AddLiquidity.SendAsync(new AddLiquidityInput
            {
                    AmountADesired = 100_00000000,
                    AmountBDesired = 50_00000000,
                    AmountAMin = 1,
                    AmountBMin = 1,
                    SymbolA = SymbolLink,
                    SymbolB = SymbolUsdt,
                    Deadline = Timestamp.FromDateTime(DateTime.UtcNow.Add(new TimeSpan(0,0,3))),
                    To = Tom
            });

            var pair2Balance = await lpTokenTomStub.GetBalance.CallAsync(new Token.GetBalanceInput
            {
                Owner = Tom,
                Symbol = GetTokenPairSymbol(SymbolLink,SymbolUsdt)
            });

            pair2Balance.Amount.ShouldBe(7071067811L);
            
            await lpTokenTomStub.Transfer.SendAsync(new Token.TransferInput
            {
                To = Owner,
                Amount = 50_00000000,
                Symbol = GetTokenPairSymbol(SymbolLink,SymbolUsdt)
            });
            
            // aave-link
            await commonTokenTomStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 200_00000000,
                Spender = SwapContractAddress,
                Symbol = SymbolAave
            });

            await commonTokenTomStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 400_00000000,
                Spender = SwapContractAddress,
                Symbol = SymbolLink
            });

            await tomSwapStub.AddLiquidity.SendAsync(new AddLiquidityInput
            {
                AmountADesired = 200_00000000,
                AmountBDesired = 400_00000000,
                AmountAMin = 1,
                AmountBMin = 1,
                SymbolA = SymbolAave,
                SymbolB = SymbolLink,
                Deadline = Timestamp.FromDateTime(DateTime.UtcNow.Add(new TimeSpan(0, 0, 3))),
                To = Tom
            });

            var pair3Balance = await lpTokenTomStub.GetBalance.CallAsync(new Token.GetBalanceInput
            {
                Owner = Tom,
                Symbol = GetTokenPairSymbol(SymbolAave, SymbolLink)
            });
            pair3Balance.Amount.ShouldBe(28284271247L);
            
            await lpTokenTomStub.Transfer.SendAsync(new Token.TransferInput
            {
                Amount = 200_00000000,
                Symbol = GetTokenPairSymbol(SymbolAave,SymbolLink),
                To = Owner
            });
            
            var reserves = await tomSwapStub.GetReserves.CallAsync(new GetReservesInput
            {
                SymbolPair = { $"{SymbolLink}-{SymbolElff}",$"{SymbolLink}-{SymbolUsdt}",$"{SymbolAave}-{SymbolLink}"}
            });
            reserves.Results[0].ReserveA.ShouldBe(100_00000000);
            reserves.Results[0].ReserveB.ShouldBe(50_00000000);
            reserves.Results[1].ReserveA.ShouldBe(100_00000000);
            reserves.Results[1].ReserveB.ShouldBe(50_00000000);
        }
        
        private async Task CreatePairs()
        {
            var tomSwapStub = GetSwapContractStub(TomPair);
            await tomSwapStub.CreatePair.SendAsync(new CreatePairInput
            {
                SymbolPair = $"{SymbolLink}-{SymbolElff}"
            });

            await tomSwapStub.CreatePair.SendAsync(new CreatePairInput
            {
                SymbolPair = $"{SymbolLink}-{SymbolUsdt}"
            });

            await tomSwapStub.CreatePair.SendAsync(new CreatePairInput
            {
                SymbolPair = $"{SymbolAave}-{SymbolLink}"
            });
            
            var pairList = await tomSwapStub.GetPairs.CallAsync(new Empty());
            pairList.Value.ShouldContain($"{SymbolElff}-{SymbolLink}");
            pairList.Value.ShouldContain($"{SymbolLink}-{SymbolUsdt}");
        }
        
        private async Task InitLpTokenContract()
        {
            var lpTokenContractStub = GetLpTokenContractStub(OwnerPair);
            await lpTokenContractStub.Initialize.SendAsync(new Token.InitializeInput
            {
                Owner = SwapContractAddress
            });
        }
        
        
        private async Task InitSwapContract()
        {
            var adminSwapStub = GetSwapContractStub(OwnerPair);
            await adminSwapStub.Initialize.SendAsync(new Swap.InitializeInput
            {
                Admin = Owner,
                AwakenTokenContractAddress = LpTokenContractAddress
            });
            await adminSwapStub.SetFeeRate.SendAsync(new Int64Value
            {
                Value = 30
            });

            var feeRate = await adminSwapStub.GetFeeRate.CallAsync(new Empty());
            feeRate.Value.ShouldBe(30);
        }
        
        private async Task InitAccounts()
        {
            OwnerPair = SampleAccount.Accounts.First().KeyPair;
            Owner = Address.FromPublicKey(OwnerPair.PublicKey);

            TomPair = SampleAccount.Accounts[1].KeyPair;
            Tom = Address.FromPublicKey(TomPair.PublicKey);

            KittyPair = SampleAccount.Accounts[2].KeyPair;
            Kitty = Address.FromPublicKey(KittyPair.PublicKey);

            ReceiverPair = SampleAccount.Accounts[3].KeyPair;
            Receiver = Address.FromPublicKey(ReceiverPair.PublicKey);
            
        }

        private async Task InitCommonTokens()
        {
            await CreateToken(SymbolUsdt, Owner, TotalSupply);
            await CreateToken(SymbolElff, Owner, TotalSupply);
            await CreateToken(SymbolLink, Owner, TotalSupply);
            await CreateToken(SymbolAave, Owner, TotalSupply);
        }

        private async Task DistributeToken(string symbol,long amount,Address to)
        {
            var adminCommonTokenStub = GetCommonTokenContractStub(OwnerPair);
            await adminCommonTokenStub.Transfer.SendAsync(new TransferInput()
            {
                Amount = amount,
                Symbol = symbol,
                To = to
            });


            var balanceCallAsync = await adminCommonTokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = to,
                Symbol = symbol
            });
            balanceCallAsync.Balance.ShouldBe(amount);
        }

        private async Task CreateToken(string symbol, Address issuer, long totalSupply)
        {
            var commonTokenStub = GetCommonTokenContractStub(OwnerPair);
            await commonTokenStub.Create.SendAsync(new CreateInput
            {
                Decimals = 8,
                Symbol = symbol,
                Issuer = issuer,
                IsBurnable = true,
                TokenName = symbol,
                TotalSupply = totalSupply
            });

            var res = await commonTokenStub.Issue.SendAsync(new IssueInput
            {
                Amount = totalSupply,
                Symbol = symbol,
                To = Owner
            });
            res.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var balanceCallAsync = await commonTokenStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = issuer,
                Symbol = symbol
            });
            balanceCallAsync.Balance.ShouldBe(totalSupply);

            await DistributeToken(symbol, 1000_00000000, Tom);
            await DistributeToken(symbol, 1000_00000000, Kitty);
        }

        /**
         * start init
         */
        internal async Task Initialize()
        {
            await InitAccounts();
            await InitCommonTokens();
            await InitLpTokenContract();
            await InitSwapContract();
            await CreatePairs();
            await AddLiquidity();
            await InitSwapExchangeContract();
        }

        private async Task InitSwapExchangeContract()
        {
            var swapExchangeOwnerStub = GetSwapExchangeContractStub(OwnerPair);
            await swapExchangeOwnerStub.Initialize.SendAsync(new InitializeInput
            {
                Onwer = Owner,
                Receivor = Receiver,
                SwapContract = SwapContractAddress,
                TargetToken = SymbolUsdt,
                LpTokenContract = LpTokenContractAddress
            });
        }


        internal string GetTokenPairSymbol(String tokenA, string tokenB)
        {
            var symbols = SortSymbols(tokenA, tokenB);
            return $"ALP {symbols[0]}-{symbols[1]}";
        }

        private string[] SortSymbols(params string[] symbols)
        {
            return symbols.OrderBy(s => s).ToArray();
        }
        
        internal string ExtractTokenPairFromSymbol(string symbol)
        {
            // ReSharper disable once PossibleNullReferenceException
            return symbol.StartsWith("ALP")
                ? symbol.Substring(symbol.IndexOf("ALP", StringComparison.Ordinal) + "ALP".Length).Trim()
                : symbol.Trim();
        }

        internal string[] ExtractTokensFromTokenPair(string tokenPair)
        {   
            return SortSymbols(tokenPair.Split('-'));
        }
        
        //===============================================================================================

        internal SwapExchangeContractContainer.SwapExchangeContractStub GetSwapExchangeContractStub(
            ECKeyPair senderKeyPair)
        {
            return GetTester<SwapExchangeContractContainer.SwapExchangeContractStub>(DAppContractAddress,
                senderKeyPair);
        }

        internal TokenContractContainer.TokenContractStub GetCommonTokenContractStub(ECKeyPair pair)
        {
            return GetTester<TokenContractContainer.TokenContractStub>(TokenContractAddress, pair);
        }

        internal Awaken.Contracts.Token.TokenContractContainer.TokenContractStub GetLpTokenContractStub(ECKeyPair pair)
        {
            return GetTester<Awaken.Contracts.Token.TokenContractContainer.TokenContractStub>(LpTokenContractAddress,
                pair);
        }

        internal AwakenSwapContractContainer.AwakenSwapContractStub GetSwapContractStub(ECKeyPair pair)
        {
            return GetTester<AwakenSwapContractContainer.AwakenSwapContractStub>(SwapContractAddress, pair);
        }

        //Addrerss
        private Address LpTokenContractAddress => GetAddress(AwakenTokenContractAddressNameProvider.StringName);
        private Address SwapContractAddress => GetAddress(AwakenSwapContractAddressNameProvider.StringName);
        
        
        public static T DeserializeAElfEvent<T>(LogEvent logEvent) where T : IEvent<T>, new()
        {
            var message = new T();
            message.MergeFrom(logEvent);
            return message;
        }
    }
    
    public static class EventExtension
    {
        public static void MergeFrom<T>(this T eventData, LogEvent log) where T : IEvent<T>
        {
            foreach (var bs in log.Indexed)
            {
                eventData.MergeFrom(bs);
            }

            eventData.MergeFrom(log.NonIndexed);
        }
    }
}