using System;
using Consensus;
using BlockChain.Store;
using Store;
using Infrastructure;
using System.Collections.Generic;
using Microsoft.FSharp.Collections;
using System.Linq;
using BlockChain.Data;
using System.Threading.Tasks.Dataflow;
using System.Threading.Tasks;
using System.Collections;

namespace BlockChain
{
	public class BlockChain : ResourceOwner
	{
#if TEST
		const int COINBASE_MATURITY = 0;
#else
		const int COINBASE_MATURITY = 100;
#endif

		readonly TimeSpan OLD_TIP_TIME_SPAN = TimeSpan.FromMinutes(5);
		readonly DBContext _DBContext;

		public MemPool memPool { get; set; }
		public UTXOStore UTXOStore { get; set; }
		public BlockStore BlockStore { get; set; }
		public BlockNumberDifficulties BlockNumberDifficulties { get; set; }
		public ChainTip ChainTip { get; set; }
		public BlockTimestamps Timestamps { get; set; }
		public byte[] GenesisBlockHash { get; set; }

		public enum TxResultEnum
		{
			Accepted,
			OrphanMissingInputs, // don't ban peer
			OrphanIC, // don't ban peer
			Invalid, // ban peer or inform wallet
			DoubleSpend, //don't ban peer
			Known
		}

		public enum IsTxOrphanResult
		{
			NotOrphan,
			Orphan,
			Invalid,
		}

		public enum IsContractGeneratedTxResult
		{
			NotContractGenerated,
			ContractGenerated,
			Invalid,
		}

#if DEBUG
		// for unit tests
		public void WaitDbTxs()
		{
			_DBContext.Wait();
		}
#endif

		public BlockChain(string dbName, byte[] genesisBlockHash)
		{
			_DBContext = new DBContext(dbName);
			memPool = new MemPool();
			UTXOStore = new UTXOStore();
			BlockStore = new BlockStore();
			BlockNumberDifficulties = new BlockNumberDifficulties();
			ChainTip = new ChainTip();
			Timestamps = new BlockTimestamps();
			GenesisBlockHash = genesisBlockHash;

			using (var context = _DBContext.GetTransactionContext())
			{
				var chainTip = ChainTip.Context(context).Value;

				//TODO: check if makred as main?
				Tip = chainTip == null ? null : BlockStore.GetBlock(context, chainTip);
			}

			InitBlockTimestamps();

			var buffer = new BufferBlock<QueueAction>();
			QueueAction.Target = buffer;
			var consumer = ConsumeAsync(buffer);

			OwnResource(_DBContext);
		}

		async Task ConsumeAsync(ISourceBlock<QueueAction> source)
		{
			while (await source.OutputAvailableAsync())
			{
				var action = source.Receive();

				try
				{
					if (action is HandleBlockAction)
						HandleBlock(action as HandleBlockAction);
					else if (action is HandleOrphansOfTxAction)
						HandleOrphansOfTransaction(action as HandleOrphansOfTxAction);
				}
				catch (Exception e)
				{
					BlockChainTrace.Error("action consumer", e);
				}
			}
		}

		void HandleOrphansOfTransaction(HandleOrphansOfTxAction a)
		{
			using (var dbTx = _DBContext.GetTransactionContext())
			{
				lock (memPool)
				{
					memPool.OrphanTxPool.GetOrphansOf(a.TxHash).ToList().ForEach(t =>
					{
						TransactionValidation.PointedTransaction ptx;
						var tx = memPool.OrphanTxPool[t];

						switch (IsOrphanTx(dbTx, tx, out ptx))
						{
							case IsTxOrphanResult.Orphan:
								return;
							case IsTxOrphanResult.Invalid:
								BlockChainTrace.Information("invalid orphan tx removed from orphans", tx);
								break;
							case IsTxOrphanResult.NotOrphan:
								if (!memPool.TxPool.ContainsInputs(tx))
								{
									if (IsValidTransaction(dbTx, ptx))
									{
										BlockChainTrace.Information("unorphaned tx added to mempool", tx);
										memPool.TxPool.Add(t, ptx);
									}
									else
									{
										BlockChainTrace.Information("invalid orphan tx removed from orphans", tx);
									}
								}
								else
								{
									BlockChainTrace.Information("double spent orphan tx removed from orphans", tx);
								}
								break;
						}

						memPool.OrphanTxPool.RemoveDependencies(t);
					});
				}
			}
		}

		/// <summary>
		/// Handles a new transaction from network or wallet. 
		/// </summary>
		public TxResultEnum HandleTransaction(Types.Transaction tx)
		{
			using (var dbTx = _DBContext.GetTransactionContext())
			{
				TransactionValidation.PointedTransaction ptx;
				var txHash = Merkle.transactionHasher.Invoke(tx);

				lock (memPool)
				{
					if (memPool.TxPool.Contains(txHash))
					{
						BlockChainTrace.Information("Tx already in mempool", txHash);
						return TxResultEnum.Known;
					}

					if (BlockStore.TxStore.ContainsKey(dbTx, txHash))
					{
						BlockChainTrace.Information("Tx already in store", txHash);
						return TxResultEnum.Known;
					}

					if (memPool.TxPool.ContainsInputs(tx))
					{
						BlockChainTrace.Information("Mempool contains spending input", tx);
						return TxResultEnum.DoubleSpend;
					}

					switch (IsOrphanTx(dbTx, tx, out ptx))
					{
						case IsTxOrphanResult.Orphan:
							BlockChainTrace.Information("tx added as orphan", tx);
							memPool.OrphanTxPool.Add(txHash, tx);
							return TxResultEnum.OrphanMissingInputs;
						case IsTxOrphanResult.Invalid:
							BlockChainTrace.Information("tx contains invalid reference(s)", tx);
							return TxResultEnum.Invalid;
					}

					if (!IsCoinbaseTxsValid(dbTx, ptx))
					{
						BlockChainTrace.Information("referenced coinbase immature", tx);
						return TxResultEnum.Invalid;
					}

					byte[] contractHash;
					switch (IsContractGeneratedTx(ptx, out contractHash))
					{
						case IsContractGeneratedTxResult.NotContractGenerated:
							if (!IsValidTransaction(dbTx, ptx))
							{
								BlockChainTrace.Information("tx invalid - universal", ptx);
								return TxResultEnum.Invalid;
							}
							break;
						case IsContractGeneratedTxResult.ContractGenerated:
							if (!new ActiveContractSet().IsActive(dbTx, contractHash))
							{
								BlockChainTrace.Information("tx added to ICTx mempool", tx);
								BlockChainTrace.Information(" of contract", contractHash);
								memPool.TxPool.ICTxPool.Add(txHash, ptx);
								return TxResultEnum.OrphanIC;
							}
							if (!IsValidTransaction(dbTx, ptx))
							{
								BlockChainTrace.Information("tx invalid - universal", ptx);
								return TxResultEnum.Invalid;
							}
							if (!IsContractGeneratedTransactionValid(dbTx, ptx, contractHash))
							{
								BlockChainTrace.Information("tx invalid - invalid contract", ptx);
								return TxResultEnum.Invalid;
							}
							break;
						case IsContractGeneratedTxResult.Invalid:
							BlockChainTrace.Information("tx invalid - input locks", tx);
							return TxResultEnum.Invalid;
					}

					BlockChainTrace.Information("tx added to mempool", ptx);
					memPool.TxPool.Add(txHash, ptx);
				}
				return TxResultEnum.Accepted;
			}
		}

		bool IsCoinbaseTxsValid(TransactionContext dbTx, TransactionValidation.PointedTransaction ptx)
		{
			var currentHeight = Tip == null ? 0 : Tip.Value.header.blockNumber;

			foreach (var refTx in ptx.pInputs.Select(t => t.Item1.txHash))
			{
				Types.BlockHeader refTxBk;
				if (BlockStore.IsCoinbaseTx(dbTx, refTx, out refTxBk))
				{
					if (refTxBk.blockNumber - currentHeight < COINBASE_MATURITY)
					{
						return false;
					}
				}
			}

			return true;
		}

		/// <summary>
		/// Handles a block from network.
		/// </summary>
		/// <returns><c>true</c>, if new block was acceped, <c>false</c> rejected.</returns>
		public BlockVerificationHelper.BkResultEnum HandleBlock(Types.Block bk)
		{
			return HandleBlock(new HandleBlockAction(bk));
		}

		BlockVerificationHelper.BkResultEnum HandleBlock(HandleBlockAction a)
		{
			BlockVerificationHelper action = null;

			using (var dbTx = _DBContext.GetTransactionContext())
			{
				action = new BlockVerificationHelper(
					this,
					dbTx,
					a.BkHash,
					a.Bk,
					a.IsOrphan
				);
				
				switch (action.Result)
				{
					case BlockVerificationHelper.BkResultEnum.AcceptedOrphan:
						dbTx.Commit();
						break;
					case BlockVerificationHelper.BkResultEnum.Accepted:
						UpdateMempool(dbTx, action.ConfirmedTxs, action.UnconfirmedTxs);
						break;
					case BlockVerificationHelper.BkResultEnum.Rejected:
						return action.Result;
				}

				foreach (var _bk in BlockStore.Orphans(dbTx, a.BkHash))
				{
					new HandleBlockAction(_bk.Key, _bk.Value, true).Publish();
				}
			}

			action.QueuedActions.ForEach(t =>
			{
				if (t is MessageAction)
					(t as MessageAction).Message.Publish();
				else
					a.Publish();
			});

			return action.Result;
		}

		void UpdateMempool(TransactionContext dbTx, HashDictionary<TransactionValidation.PointedTransaction> confirmedTxs, HashDictionary<Types.Transaction> unconfirmedTxs)
		{
			lock (memPool)
			{
				dbTx.Commit();

				var activeContracts = new ActiveContractSet().Keys(dbTx);
				activeContracts.AddRange(memPool.ContractPool.Keys);
				               
				EvictTxToMempool(dbTx, unconfirmedTxs);
				RemoveConfirmedTxFromMempool(confirmedTxs);

				var utxos = new List<Tuple<Types.Outpoint, Types.Output>>();

				foreach (var item in new UTXOStore().All(dbTx, null, false).Where(t => t.Value.@lock is Types.OutputLock.ContractLock))
				{
					utxos.Add(new Tuple<Types.Outpoint, Types.Output>(item.Key, item.Value));
				}

				memPool.ICTxPool.Purge(activeContracts, utxos, Tip.Value.header);
				memPool.TxPool.MoveToICTxPool(activeContracts);
			}
		}

		void EvictTxToMempool(TransactionContext dbTx, HashDictionary<Types.Transaction> unconfirmedTxs)
		{
			foreach (var tx in unconfirmedTxs)
			{
				TransactionValidation.PointedTransaction ptx = null;

				switch (IsOrphanTx(dbTx, tx.Value, out ptx))
				{
					case IsTxOrphanResult.NotOrphan:
						//if (!memPool.TxPool.ContainsInputs(TransactionValidation.unpoint(ptx)))
						//{
						BlockChainTrace.Information("tx evicted to mempool", ptx);
						memPool.TxPool.Add(tx.Key, ptx);
						new TxMessage(tx.Key, ptx, TxStateEnum.Unconfirmed).Publish();
						//}
						//else
						//{
						//	BlockChainTrace.Information("double spent tx not evicted to mempool", ptx);
						//	new TxMessage(tx.Key, ptx, TxStateEnum.Invalid).Publish();
						//}
						break;
					case IsTxOrphanResult.Orphan: // is a double-spend
						BlockChainTrace.Information("double spent tx not evicted to mempool", tx.Key);
						memPool.TxPool.RemoveDependencies(tx.Key);
						new TxMessage(tx.Key, null, TxStateEnum.Invalid).Publish();
						break;
				}
			}
		}

		void RemoveConfirmedTxFromMempool(HashDictionary<TransactionValidation.PointedTransaction> confirmedTxs)
		{
			var spentOutputs = new List<Types.Outpoint>(); //TODO sort - efficiency

			foreach (var ptx in confirmedTxs.Values)
			{
				spentOutputs.AddRange(ptx.pInputs.Select(t=>t.Item1));
			}

			foreach (var item in confirmedTxs)
			{
				// check in ICTxs as well?
				if (memPool.TxPool.Contains(item.Key))
				{
					BlockChainTrace.Information("same tx removed from txpool", item.Value);
					memPool.TxPool.Remove(item.Key);
					memPool.ContractPool.Remove(item.Key);
				}
				else
				{
					new HandleOrphansOfTxAction(item.Key).Publish(); // assume tx is unseen. try to unorphan
				}

				// Make list of **keys** in txpool and ictxpool
				// for each key in list, check if Double Spent. Remove recursively.
				// RemoveIfDoubleSpent is recursive over all pools, then sends a RemoveRef to ContractPool
				memPool.TxPool.RemoveDoubleSpends(spentOutputs);
				memPool.ICTxPool.RemoveDoubleSpends(spentOutputs);

				//new TxMessage(item.Key, item.Value, TxStateEnum.Confirmed).Publish();
			}
		}

		public IsTxOrphanResult IsOrphanTx(TransactionContext dbTx, Types.Transaction tx, out TransactionValidation.PointedTransaction ptx)
		{
			var outputs = new List<Types.Output>();

			ptx = null;

			foreach (Types.Outpoint input in tx.inputs)
			{
				if (UTXOStore.ContainsKey(dbTx, input))
				{
					outputs.Add(UTXOStore.Get(dbTx, input).Value);
				} else if (memPool.TxPool.Contains(input.txHash))
				{
					if (input.index < memPool.TxPool[input.txHash].outputs.Length)
					{
						outputs.Add(memPool.TxPool[input.txHash].outputs[(int)input.index]);
					}
					else
					{
						BlockChainTrace.Information("can't construct ptx", tx);
						return IsTxOrphanResult.Invalid;
					}
				}
			}

			if (outputs.Count < tx.inputs.Count())
			{
				return IsTxOrphanResult.Orphan;
			}

			ptx = TransactionValidation.toPointedTransaction(
				tx,
				ListModule.OfSeq<Types.Output>(outputs)
			);

			return IsTxOrphanResult.NotOrphan;
		}

		public bool IsValidTransaction(TransactionContext dbTx, TransactionValidation.PointedTransaction ptx)
		{
			//Verify crypto signatures for each input; reject if any are bad


			//Using the referenced output transactions to get input values, check that each input value, as well as the sum, are in legal money range
			//Reject if the sum of input values < sum of output values


			//for (var i = 0; i < ptx.pInputs.Length; i++)
			//{
			//	if (!TransactionValidation.validateAtIndex(ptx, i))
			//		return false;
			//}

			return true;
		}

		public static IsContractGeneratedTxResult IsContractGeneratedTx(TransactionValidation.PointedTransaction ptx, out byte[] contractHash)
		{
			contractHash = null;

			foreach (var input in ptx.pInputs)
			{
				if (input.Item2.@lock.IsContractLock)
				{
					if (contractHash == null)
						contractHash = ((Types.OutputLock.ContractLock)input.Item2.@lock).contractHash;
					else if (!contractHash.SequenceEqual(((Types.OutputLock.ContractLock)input.Item2.@lock).contractHash))
						return IsContractGeneratedTxResult.Invalid;

					else if (!contractHash.SequenceEqual(((Types.OutputLock.ContractLock)input.Item2.@lock).contractHash))
					{
						BlockChainTrace.Information("Unexpected contactHash", contractHash);
						return IsContractGeneratedTxResult.Invalid;
					}
				}
			}

			return contractHash == null ? IsContractGeneratedTxResult.NotContractGenerated : IsContractGeneratedTxResult.ContractGenerated;
		}


		// TODO replace with two functions:
		// IsContractActive(contractHash), which checks if the contract is in the ACS on disk or in the contractpool;
		// bool IsContractGeneratedTransactionValid(dbtx, contracthash, ptx), which raises an exception if called with a missing contract
		public static bool IsContractGeneratedTransactionValid(TransactionContext dbTx, TransactionValidation.PointedTransaction ptx, byte[] contractHash)
		{
			/// TODO: cache
			var utxos = new List<Tuple<Types.Outpoint, Types.Output>>();

			foreach (var item in new UTXOStore().All(dbTx, null, false).Where(t =>
			{
				var contractLock = t.Value.@lock as Types.OutputLock.ContractLock;
				return contractLock != null && contractLock.contractHash.SequenceEqual(contractHash); 
			}))
			{
				utxos.Add(new Tuple<Types.Outpoint, Types.Output>(item.Key, item.Value));
			}

			var chainTip = new ChainTip().Context(dbTx).Value;
			var tipBlockHeader = chainTip == null ? null : new BlockStore().GetBlock(dbTx, chainTip).Value.header;
			return ContractHelper.IsTxValid(ptx, contractHash, utxos, tipBlockHeader);
		}

		public TransactionContext GetDBTransaction()
		{
			return _DBContext.GetTransactionContext();
		}

		public void InitBlockTimestamps()
		{
			if (Tip != null)
			{
				var timestamps = new List<long>();
				var itr = Tip == null ? null : Tip.Value;

				while (itr != null && timestamps.Count < BlockTimestamps.SIZE)
				{
					timestamps.Add(itr.header.timestamp);
					itr = itr.header.parent.Length == 0 ? null : GetBlock(itr.header.parent);
				}
				Timestamps.Init(timestamps.ToArray());
			}
		}

		public bool IsTipOld
		{
			get
			{
				return Tip == null ? true : DateTime.Now - DateTime.FromBinary(Tip.Value.header.timestamp) > OLD_TIP_TIME_SPAN;
			}
		}

		//TODO: refactor
		public Keyed<Types.Block> Tip { get; set; }

		public Types.Transaction GetTransaction(byte[] key) //TODO: make concurrent
		{
			if (memPool.TxPool.Contains(key))
			{
				return TransactionValidation.unpoint(memPool.TxPool[key]);
			}
			else
			{
				using (TransactionContext context = _DBContext.GetTransactionContext())
				{
					if (BlockStore.TxStore.ContainsKey(context, key))
					{
						return BlockStore.TxStore.Get(context, key).Value;
					}
				}
			}

			return null;
		}

		//TODO: should asset that the block came from main?
		public Types.Block GetBlock(byte[] key)
		{
			using (TransactionContext context = _DBContext.GetTransactionContext())
			{
				var location = BlockStore.GetLocation(context, key);

				if (location == LocationEnum.Main || location == LocationEnum.Genesis)
				{
					var bk = BlockStore.GetBlock(context, key);

					return bk == null ? null : bk.Value;
				}

				return null;
			}
		}

		// TODO: use linq, return enumerator, remove predicate
		public void GetUTXOSet(Func<Types.Output, bool> predicate, out HashDictionary<List<Types.Output>> txOutputs, out HashDictionary<Types.Transaction> txs)
		{
			txOutputs = new HashDictionary<List<Types.Output>>();
			txs = new HashDictionary<Types.Transaction>();

			using (TransactionContext context = _DBContext.GetTransactionContext())
			{
				foreach (var item in UTXOStore.All(context, predicate, true))
				{
					if (!txOutputs.ContainsKey(item.Key.txHash))
					{
						txOutputs[item.Key.txHash] = new List<Types.Output>();
					}

					txOutputs[item.Key.txHash].Add(item.Value);
					txs[item.Key.txHash] = BlockStore.TxStore.Get(context, item.Key.txHash).Value;
				}
			}
		}
	}
}
