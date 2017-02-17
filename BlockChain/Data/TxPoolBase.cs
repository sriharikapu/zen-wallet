using System.Collections.Generic;
using Consensus;
using System.Linq;

namespace BlockChain.Data
{
	public abstract class TxPoolBase : HashDictionary<TransactionValidation.PointedTransaction>
	{
		public ICTxPool ICTxPool { get; set; }
		public ContractPool ContractPool { get; set; }
		public OrphanTxPool OrphanTxPool { get; set; }

		public bool Contains(byte[] txHash)
		{
			return ContainsKey(txHash);
		}

		public new bool Remove(byte[] txHash)
		{
			new MessageAction(new NewTxMessage(txHash, TxStateEnum.Invalid)).Publish();
			return base.Remove(txHash);
		}

		public void RemoveDoubleSpends(List<Types.Outpoint> spentOutputs) //TODO sort - efficiency
		{
			Keys.ToList().ForEach(t =>
			{
				if (Contains(t))
				{
					if (this[t].pInputs.Select(_t => _t.Item1).Count(_t => spentOutputs.Contains(_t)) > 0)
					{
						BlockChainTrace.Information("double-spending tx removed from txpool");
						RemoveDependencies(t);
						ContractPool.Remove(t);
						OrphanTxPool.RemoveDependencies(t);
						ICTxPool.RemoveDependencies(t);
					}
				}
			});
		}

		public void RemoveDependencies(byte[] txHash)
		{
			if (Contains(txHash))
			{
				BlockChainTrace.Information("invalidated tx removed from txpool");
				new MessageAction(new NewTxMessage(txHash, TxStateEnum.Invalid)).Publish();
				Remove(txHash);
				foreach (var dep in GetDependencies(txHash))
				{
					RemoveDependencies(dep);
				}
			}

			ContractPool.Remove(txHash);
			OrphanTxPool.Remove(txHash);
		}

		protected IEnumerable<byte[]> GetDependencies(byte[] txHash)
		{
			foreach (var item in this)
			{
				if (item.Value.pInputs.Select(t => t.Item1).Count(t => t.txHash.SequenceEqual(txHash)) > 0)
				{
					yield return item.Key;
				}
			}
		}
	}
}