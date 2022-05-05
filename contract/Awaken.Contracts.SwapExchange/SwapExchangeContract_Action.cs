using System;
using System.Linq;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using Awaken.Contracts.Swap;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using ApproveInput = Awaken.Contracts.Token.ApproveInput;
using TransferFromInput = Awaken.Contracts.Token.TransferFromInput;

namespace Awaken.Contracts.SwapExchangeContract
{
    /// <summary>
    /// The C# implementation of the contract defined in swap_exchange_contract.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public partial class SwapExchangeContract
    {
        public override Empty SetSwapToTargetTokenThreshold(Thresholdinput input)
        {
            OnlyOwner();
            if (input.LpTokenThreshold > 0)
            {
                State.LpTokenThreshold.Value = input.LpTokenThreshold;
            }

            if (input.CommonTokenThreshold > 0)
            {
                State.CommonTokenThreshold.Value = input.CommonTokenThreshold;
            }
            return new Empty();
        }

        /**
         * SetTargetToken
         */
        public override Empty SetTargetToken(StringValue input)
        {
            OnlyOwner();
            var tokenInfo = State.CommonTokenContract.GetTokenInfo.Call(new GetTokenInfoInput
            {
                Symbol = input.Value
            });
            Assert(tokenInfo != null && tokenInfo.Symbol.Equals(input.Value), $"Token {input.Value} not exist.");
            State.TargetToken.Value = input.Value;
            return new Empty();
        }

        /**
         * SetReceivor
         */
        public override Empty SetReceivor(Address input)
        {
            OnlyOwner();
            Assert(input != null, "Invalid input.");
            State.Receivor.Value = input;
            return new Empty();
        }

        /**
         * SwapCommonTokens
         */
        public override Empty SwapCommonTokens(SwapTokensInput input)
        {
            OnlyOwner();
            var tokensInfo = input.SwapTokenList.TokensInfo;
            Assert(tokensInfo.Count > 0, "Invalid params.");
            State.CumulativeTokenList.Value = new TokenList();

            //transfer in
            foreach (var token in tokensInfo)
            {
                bool result = CheckCommonTokenThresholdLimit(token, input.PathMap);
                if (!result)
                {
                    Context.Fire(new SwapResultEvent
                    {
                        Symbol = token.TokenSymbol,
                        Result = false,
                        IsLptoken = false,
                        Amount = token.Amount
                    });
                    continue;
                }

                State.CumulativeTokenList.Value.TokensInfo.Add(token);
                State.CommonTokenContract.TransferFrom.Send(new AElf.Contracts.MultiToken.TransferFromInput
                {
                    Amount = token.Amount,
                    From = Context.Sender,
                    Symbol = token.TokenSymbol,
                    To = Context.Self
                });
            }

            Context.SendInline(Context.Self, nameof(SwapTokensInline), new SwapTokensInlineInput
            {
                PathMap = {input.PathMap}
            });
            return new Empty();
        }

        private bool CheckCommonTokenThresholdLimit(Token token, MapField<string, Path> pathMap)
        {
            Assert(!string.IsNullOrEmpty(State.TargetToken.Value), "Target token not config.");
            Assert(State.CommonTokenThreshold.Value > 0, "Common token threshold not config.");
            var path = HandlePath(token.TokenSymbol, pathMap[token.TokenSymbol]);
            var result = ForecastSwapResult(token.Amount, path);
            return result >= State.CommonTokenThreshold.Value;
        }


        /**
         * SwapLpTokens
         */
        public override Empty SwapLpTokens(SwapTokensInput input)
        {
            OnlyOwner();
            var tokensInfo = input.SwapTokenList.TokensInfo;
            Assert(tokensInfo.Count > 0, "Invalid params.");
            State.CumulativeTokenList.Value = new TokenList();
            foreach (var token in tokensInfo)
            {
                bool result = CheckLpTokenThresholdLimit(token, input.PathMap);
                if (!result)
                {
                    Context.Fire(new SwapResultEvent
                    {
                        Amount = token.Amount,
                        Result = false,
                        Symbol = token.TokenSymbol,
                        IsLptoken = true
                    });
                    continue;
                }

                State.LpTokenContract.TransferFrom.Send(new TransferFromInput
                {
                    From = Context.Sender,
                    Amount = token.Amount,
                    Symbol = token.TokenSymbol,
                    To = Context.Self
                });

                Context.SendInline(Context.Self, nameof(RemoveLiquidityInline), new RemoveLiquidityInlineInput
                {
                    Token = token
                });
            }

            Context.SendInline(Context.Self, nameof(SwapTokensInline), new SwapTokensInlineInput
            {
                PathMap = {input.PathMap}
            });

            return new Empty();
        }

        /**
         *  Check whether the token meets the swap threshold
         */
        private bool CheckLpTokenThresholdLimit(Token token, MapField<string, Path> pathMap)
        {
            Assert(!string.IsNullOrEmpty(State.TargetToken.Value), "Target token not config.");
            Assert(State.LpTokenThreshold.Value > 0, "LpToken threshold not config.");
            var tokenPair = ExtractTokenPairFromSymbol(token.TokenSymbol);
            //Get total supply.
            var totalSupplyObject = State.SwapContract.GetTotalSupply.Call(new StringList
            {
                Value = {tokenPair}
            });
            var totalSupply = totalSupplyObject.Results.First().TotalSupply;
            Assert(tokenPair.Equals(totalSupplyObject.Results.First().SymbolPair),
                $"Token pair {tokenPair} not match.");
            // Get reserves.  
            var reserves = State.SwapContract.GetReserves.Call(new GetReservesInput
            {
                SymbolPair = {tokenPair}
            });
            var tokenA = reserves.Results.First().SymbolA;
            long resultA;
            if (!tokenA.Equals(State.TargetToken.Value))
            {
                var amountA = new BigIntValue(token.Amount).Mul(reserves.Results.First().ReserveA).Div(totalSupply);
                var tokenAPath = HandlePath(tokenA, pathMap[tokenA]);
                resultA = ForecastSwapResult(BigIntValueToLong(amountA), tokenAPath);
            }
            else
            {
                resultA = reserves.Results.First().ReserveA;
            }
            
            var tokenB = reserves.Results.First().SymbolB;
            long resultB;
            if (!tokenB.Equals(State.TargetToken.Value))
            {
                var amountB = new BigIntValue(token.Amount).Mul(reserves.Results.First().ReserveB).Div(totalSupply);
                var tokenBPath = HandlePath(tokenB, pathMap[tokenB]);
                resultB = ForecastSwapResult(BigIntValueToLong(amountB), tokenBPath);
            }
            else
            {
                resultB = reserves.Results.First().ReserveB;
            }

            return resultA >= State.LpTokenThreshold.Value &&
                   resultB >= State.LpTokenThreshold.Value;
        }

        private long ForecastSwapResult(long amountIn, RepeatedField<string> path)
        {
            var amountsOut = State.SwapContract.GetAmountsOut.Call(new GetAmountsOutInput
            {
                AmountIn = amountIn,
                Path = {path}
            });
            return amountsOut.Amount[path.Count - 1];
        }

        /**
         * RemoveLiquidityInline
         */
        public override Empty RemoveLiquidityInline(RemoveLiquidityInlineInput input)
        {
            OnlySelf();
            State.LpTokenContract.Approve.Send(new ApproveInput
            {
                Spender = State.SwapContract.Value,
                Amount = input.Token.Amount,
                Symbol = input.Token.TokenSymbol
            });

            var tokens = ExtractTokensFromTokenPair(ExtractTokenPairFromSymbol(input.Token.TokenSymbol));
            var tokenABalanceBefore = State.CommonTokenContract.GetBalance.Call(new GetBalanceInput
            {
                Owner = Context.Self,
                Symbol = tokens[0]
            }).Balance;
            var tokenBBalanceBefore = State.CommonTokenContract.GetBalance.Call(new GetBalanceInput
            {
                Owner = Context.Self,
                Symbol = tokens[1]
            }).Balance;

            State.SwapContract.RemoveLiquidity.Send(new RemoveLiquidityInput
            {
                To = Context.Self,
                LiquidityRemove = input.Token.Amount,
                SymbolA = tokens[0],
                SymbolB = tokens[1],
                AmountAMin = 1,
                AmountBMin = 1,
                Deadline = Context.CurrentBlockTime.AddSeconds(3)
            });

            Context.SendInline(Context.Self, nameof(CumulativeTokenAmountInline), new CumulativeTokenAmountInlineInput
            {
                TokenA = tokens[0],
                TokenB = tokens[1],
                TokenABefore = tokenABalanceBefore,
                TokenBBefore = tokenBBalanceBefore
            });
            return new Empty();
        }

        /**
         * CumulativeTokenAmountInline
         */
        public override Empty CumulativeTokenAmountInline(CumulativeTokenAmountInlineInput input)
        {
            OnlySelf();
            var tokenABalanceAfter = State.CommonTokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = input.TokenA,
                Owner = Context.Self
            }).Balance;

            var tokenBBalanceAfter = State.CommonTokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = input.TokenB,
                Owner = Context.Self
            }).Balance;
            var increaseAmountTokenA = tokenABalanceAfter.Sub(input.TokenABefore);
            var increaseAmountTokenB = tokenBBalanceAfter.Sub(input.TokenBBefore);
            var tokenList = State.CumulativeTokenList.Value;
            var tokenA = tokenList.TokensInfo.FirstOrDefault(token => token.TokenSymbol.Equals(input.TokenA));
            if (tokenA != null)
            {
                tokenA.Amount = tokenA.Amount.Add(increaseAmountTokenA);
            }
            else
            {
                tokenList.TokensInfo.Add(new Token
                {
                    TokenSymbol = input.TokenA,
                    Amount = increaseAmountTokenA
                });
            }

            var tokenB = tokenList.TokensInfo.FirstOrDefault(token => token.TokenSymbol.Equals(input.TokenB));

            if (tokenB != null)
            {
                tokenB.Amount = tokenB.Amount.Add(increaseAmountTokenB);
            }
            else
            {
                tokenList.TokensInfo.Add(new Token
                {
                    TokenSymbol = input.TokenB,
                    Amount = increaseAmountTokenB
                });
            }

            return new Empty();
        }

        /**
         * SwapTokensInline
         */
        public override Empty SwapTokensInline(SwapTokensInlineInput input)
        {
            OnlySelf();
            var pathMap = input.PathMap;
            var tokensInfo = State.CumulativeTokenList.Value.TokensInfo;
            foreach (var token in tokensInfo)
            {
                if (State.TargetToken.Value.Equals(token.TokenSymbol) && token.Amount > 0)
                {
                    TransferTargetTokenToReceiver(token);
                    continue;
                }

                var path = pathMap[token.TokenSymbol];
                Assert(path != null && path.Value.Count > 0, $"{token} path lose.");
                Context.SendInline(Context.Self,nameof(SwapTokenToTargetInline),new SwapTokenToTargetInlineInput
                {
                    Token = token,
                    PathInfo = path
                });
            }
            return new Empty();
        }

        private void TransferTargetTokenToReceiver(Token token)
        {
            State.CommonTokenContract.Transfer.Send(new TransferInput
            {
                Amount = token.Amount,
                Symbol = token.TokenSymbol,
                To = State.Receivor.Value
            });
        }

        public override Empty SwapTokenToTargetInline(SwapTokenToTargetInlineInput input)
        {   
            OnlySelf();
            var token = input.Token;
            var pathPair = input.PathInfo;
            var path = HandlePath(token.TokenSymbol, pathPair);
            var result = CheckSlidingPoint(token, path, pathPair.SlipPoint, pathPair.ExpectPrice);
            if (!result)
            {
                State.CommonTokenContract.Transfer.Send(new TransferInput
                {
                    Symbol = token.TokenSymbol,
                    Amount = token.Amount,
                    To = Context.Origin
                });
                Context.Fire(new SwapResultEvent
                {
                    Amount = token.Amount,
                    Result = false,
                    Symbol = token.TokenSymbol,
                    IsLptoken = false
                });
                return new Empty();
            }

            State.CommonTokenContract.Approve.Send(new AElf.Contracts.MultiToken.ApproveInput
            {
                Spender = State.SwapContract.Value,
                Amount = token.Amount,
                Symbol = token.TokenSymbol
            });

            State.SwapContract.SwapExactTokensForTokens.Send(new SwapExactTokensForTokensInput
            {
                Path = {path},
                Channel = "Dividend pool script",
                To = State.Receivor.Value,
                AmountIn = token.Amount,
                AmountOutMin = 1,
                Deadline = Context.CurrentBlockTime.AddSeconds(3)
            });
            Context.Fire(new SwapResultEvent
            {
                Amount = token.Amount,
                Result = true,
                Symbol = token.TokenSymbol,
                IsLptoken = false
            });
            return new Empty();
        }

        

        private RepeatedField<string> HandlePath(string symbol, Path pathPair)
        {
            var path = new RepeatedField<string> {symbol};
            if (pathPair.Value[0].Contains("-"))
            {
                foreach (var pair in pathPair.Value)
                {
                    var tokens = ExtractTokensFromTokenPair(ExtractTokenPairFromSymbol(pair));
                    path.Add(tokens[0].Equals(path[path.Count - 1]) ? tokens[1] : tokens[0]);
                }
            }
            else
            {
                path = pathPair.Value;
            }

            return path;
        }
       
        private bool CheckSlidingPoint(Token token, RepeatedField<string> path, long slipPointLimit,
            BigIntValue expectPrice)
        {
            var swapOut = ForecastSwapResult(token.Amount, path);
            var acturalSlipPoint = new BigIntValue
            {
                Value = ExpansionCoefficient
            }.Mul(swapOut).Div(token.Amount).Sub(expectPrice).Mul(100).Div(expectPrice);
            return acturalSlipPoint < 0 && slipPointLimit >= acturalSlipPoint.Mul(-1) || acturalSlipPoint >= 0;
        }

        public override Empty ChangeOwner(ChangeOwnerInput input)
        {
            OnlyOwner();
            State.Owner.Value = input.Value;
            return new Empty();
        }
    }
}